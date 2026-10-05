#!/usr/bin/env python3
"""Count Wilaya-2 (DelegateID=5) request vs payment gap."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;
DECLARE @ListId INT = 5;

PRINT '=== Delegate ===';
SELECT DelegateID, DelegateName FROM dbo.Delegates WHERE DelegateID=@ListId;

PRINT '=== Request totals ===';
SELECT COUNT(*) AS RequestCount, SUM(ISNULL(Amount,0)) AS RequestSum
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId;

PRINT '=== Payment totals (no Amount on table) ===';
SELECT COUNT(*) AS PaymentCount
FROM dbo.CustomersPayments WHERE DelegateID=@ListId;

PRINT '=== All-delegates request vs payment ===';
SELECT
  (SELECT COUNT(*) FROM dbo.CustomersPaymentsRequest) AS AllRequests,
  (SELECT COUNT(*) FROM dbo.CustomersPayments) AS AllPayments;

PRINT '=== Eligibility for ListId=5 ===';
SELECT
  COUNT(*) AS Total,
  SUM(CASE WHEN EligibleForPostingAtUtc IS NULL THEN 1 ELSE 0 END) AS NullEligible,
  SUM(CASE WHEN EligibleForPostingAtUtc IS NOT NULL AND EligibleForPostingAtUtc > SYSUTCDATETIME() THEN 1 ELSE 0 END) AS FutureEligible,
  SUM(CASE WHEN EligibleForPostingAtUtc IS NOT NULL AND EligibleForPostingAtUtc <= SYSUTCDATETIME() THEN 1 ELSE 0 END) AS DueEligible,
  SUM(CASE WHEN ISNULL(AutoPostEnabled,0)=1 THEN 1 ELSE 0 END) AS AutoPostOn,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPostedYet,
  SUM(CASE WHEN ClientPaymentId IS NULL THEN 1 ELSE 0 END) AS NullClientId
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId;

PRINT '=== Request by day last 20 ===';
SELECT TOP 20 CONVERT(date, ISNULL(CreatedAtUtc, PaymentDate)) AS D,
       COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId
GROUP BY CONVERT(date, ISNULL(CreatedAtUtc, PaymentDate))
ORDER BY D DESC;

PRINT '=== Payment by day last 20 ===';
SELECT TOP 20 CONVERT(date, PaymentDate) AS D, COUNT(*) AS Cnt
FROM dbo.CustomersPayments WHERE DelegateID=@ListId
GROUP BY CONVERT(date, PaymentDate)
ORDER BY D DESC;

PRINT '=== Today Baghdad ===';
DECLARE @Today date = CONVERT(date, DATEADD(HOUR,3,SYSUTCDATETIME()));
SELECT @Today AS TodayBaghdad, SYSUTCDATETIME() AS NowUtc;

SELECT 'REQ' AS Src, COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId AND CONVERT(date, ISNULL(CreatedAtUtc,PaymentDate))=@Today
UNION ALL
SELECT 'PAY', COUNT(*), NULL
FROM dbo.CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date, PaymentDate)=@Today;

PRINT '=== Gap: requests without matching ClientPaymentId in payments ===';
SELECT COUNT(*) AS Missing
FROM dbo.CustomersPaymentsRequest r
WHERE r.DelegateID=@ListId
  AND r.ClientPaymentId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments p WHERE p.ClientPaymentId=r.ClientPaymentId);

PRINT '=== Sample due eligible not posted ===';
SELECT TOP 30
  r.CustomersPaymentsRequestID, r.CustomerID, r.Amount, r.PaymentDate,
  r.EligibleForPostingAtUtc, r.AutoPostEnabled, r.PostedToBoxAtUtc, r.ClientPaymentId, r.CreatedAtUtc
FROM dbo.CustomersPaymentsRequest r
WHERE r.DelegateID=@ListId
  AND r.PostedToBoxAtUtc IS NULL
  AND r.EligibleForPostingAtUtc IS NOT NULL
  AND r.EligibleForPostingAtUtc <= SYSUTCDATETIME()
ORDER BY r.CustomersPaymentsRequestID DESC;

PRINT '=== View remaining for due ===';
IF OBJECT_ID('dbo.View_CustomersPaymentsRequestFinal','V') IS NOT NULL
BEGIN
  SELECT
    COUNT(*) AS DueInView,
    SUM(CASE WHEN v.AmountRemaining > 0 AND v.AmountRemaining >= cpr.Amount THEN 1 ELSE 0 END) AS CanPost,
    SUM(CASE WHEN v.AmountRemaining IS NULL OR v.AmountRemaining <= 0 THEN 1 ELSE 0 END) AS NoRemaining,
    SUM(CASE WHEN v.AmountRemaining > 0 AND v.AmountRemaining < cpr.Amount THEN 1 ELSE 0 END) AS Insufficient
  FROM CustomersPaymentsRequest cpr
  JOIN View_CustomersPaymentsRequestFinal v ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
  WHERE cpr.DelegateID=@ListId
    AND cpr.AutoPostEnabled = 1
    AND cpr.PostedToBoxAtUtc IS NULL
    AND cpr.EligibleForPostingAtUtc IS NOT NULL
    AND cpr.EligibleForPostingAtUtc <= SYSUTCDATETIME();
END

PRINT '=== AddToBox / amount path for payments ===';
SELECT TOP 5 name FROM sys.tables WHERE name LIKE '%Payment%' OR name LIKE '%Box%' OR name LIKE '%AddTo%' ORDER BY name;

IF COL_LENGTH('dbo.AddToBox','Amount') IS NOT NULL
  SELECT TOP 5 * FROM dbo.AddToBox ORDER BY 1 DESC;

PRINT '=== Manual PostEligible dry-run count matching SP filter ===';
SELECT COUNT(*) AS WouldPost
FROM CustomersPaymentsRequest cpr
JOIN View_CustomersPaymentsRequestFinal v ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
WHERE cpr.AutoPostEnabled = 1
  AND cpr.PostedToBoxAtUtc IS NULL
  AND cpr.EligibleForPostingAtUtc IS NOT NULL
  AND cpr.EligibleForPostingAtUtc <= SYSUTCDATETIME()
  AND v.AmountRemaining > 0
  AND v.AmountRemaining >= cpr.Amount
  AND cpr.DelegateID=@ListId;
"""

RUNNER = r"""
import json, subprocess, tempfile, os
from pathlib import Path

def parse_cs(cs):
    parts={}
    for p in (cs or '').split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return (parts.get('server') or parts.get('data source'),
            parts.get('initial catalog') or parts.get('database'),
            parts.get('user id') or parts.get('uid'),
            parts.get('password') or parts.get('pwd'))

cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('DB', db)
sql=Path('/tmp/wilaya2_counts.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql')
os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin
    break
r=subprocess.run(cmd, capture_output=True, text=True)
print(r.stdout or '')
if r.stderr:
  print('STDERR', r.stderr[-3000:])
print('exit', r.returncode)
os.unlink(path)
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_counts.sql", "w") as f:
    f.write(SQL)
with sftp.file("/tmp/wilaya2_counts_runner.py", "w") as f:
    f.write(RUNNER)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/wilaya2_counts_runner.py", timeout=180)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR", e[-2000:])
c.close()
