#!/usr/bin/env python3
"""Compare delegate Wilaya-2 payment requests vs posted payments on Najaf."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE Arabic;

PRINT '=== Delegates matching ولاية / نجف ===';
SELECT DelegateID, DelegateName FROM dbo.Delegates
WHERE DelegateName LIKE N'%ولاية%' OR DelegateName LIKE N'%نجف%'
ORDER BY DelegateID;

DECLARE @ListId INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE DelegateName LIKE N'%ولاية%2%' OR DelegateName LIKE N'%ولاية نجف 2%' OR DelegateName = N'الولاية نجف 2'
  ORDER BY CASE WHEN DelegateName LIKE N'%2%' THEN 0 ELSE 1 END, DelegateID
);
IF @ListId IS NULL
  SET @ListId = (SELECT TOP 1 DelegateID FROM dbo.Delegates WHERE DelegateName LIKE N'%ولاية%' ORDER BY DelegateID);

PRINT '=== Chosen ListId ===';
SELECT @ListId AS ListId, (SELECT DelegateName FROM dbo.Delegates WHERE DelegateID=@ListId) AS Name;

PRINT '=== Request columns ===';
SELECT c.name FROM sys.columns c
WHERE c.object_id = OBJECT_ID('dbo.CustomersPaymentsRequest')
ORDER BY c.column_id;

PRINT '=== Payment columns (amount-like) ===';
SELECT c.name FROM sys.columns c
WHERE c.object_id = OBJECT_ID('dbo.CustomersPayments')
  AND c.name LIKE '%Amount%' OR (c.object_id = OBJECT_ID('dbo.CustomersPayments') AND c.name IN ('DelegateID','ClientPaymentId','PaymentDate','CustomerID'))
ORDER BY c.name;

PRINT '=== CustomersPaymentsRequest totals ===';
SELECT
  COUNT(*) AS RequestCount,
  SUM(ISNULL(Amount,0)) AS RequestAmountSum
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId;

PRINT '=== CustomersPaymentsRequest by day (last 14) ===';
SELECT TOP 14 CONVERT(date, ISNULL(CreatedAtUtc, PaymentDate)) AS D, COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
GROUP BY CONVERT(date, ISNULL(CreatedAtUtc, PaymentDate))
ORDER BY D DESC;

PRINT '=== CustomersPayments posted totals ===';
SELECT COUNT(*) AS PostedCount, SUM(ISNULL(Amount,0)) AS PostedAmountSum
FROM dbo.CustomersPayments
WHERE DelegateID=@ListId;

PRINT '=== CustomersPayments by day (last 14) ===';
SELECT TOP 14 CONVERT(date, PaymentDate) AS D, COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt
FROM dbo.CustomersPayments
WHERE DelegateID=@ListId
GROUP BY CONVERT(date, PaymentDate)
ORDER BY D DESC;

PRINT '=== Today Iraq ===';
DECLARE @Today date = CONVERT(date, DATEADD(HOUR,3,GETUTCDATE()));
SELECT @Today AS TodayIraq, SYSUTCDATETIME() AS NowUtc;

SELECT 'REQ_TODAY' AS Src, COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId AND CONVERT(date, ISNULL(CreatedAtUtc, PaymentDate))=@Today
UNION ALL
SELECT 'PAY_TODAY', COUNT(*), SUM(ISNULL(Amount,0))
FROM dbo.CustomersPayments WHERE DelegateID=@ListId AND CONVERT(date, PaymentDate)=@Today;

PRINT '=== Request queue / eligibility ===';
IF COL_LENGTH('dbo.CustomersPaymentsRequest','EligibleForPostingAtUtc') IS NOT NULL
BEGIN
  SELECT
    COUNT(*) AS Total,
    SUM(CASE WHEN EligibleForPostingAtUtc IS NULL THEN 1 ELSE 0 END) AS NullEligible,
    SUM(CASE WHEN EligibleForPostingAtUtc IS NOT NULL AND EligibleForPostingAtUtc > GETUTCDATE() THEN 1 ELSE 0 END) AS FutureEligible,
    SUM(CASE WHEN EligibleForPostingAtUtc IS NOT NULL AND EligibleForPostingAtUtc <= GETUTCDATE() THEN 1 ELSE 0 END) AS DueEligible,
    SUM(CASE WHEN ISNULL(AutoPostEnabled,0) = 1 THEN 1 ELSE 0 END) AS AutoPostOn,
    SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPostedYet
  FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId;
END

PRINT '=== Sample pending requests ===';
SELECT TOP 25 r.CustomersPaymentsRequestID, r.CustomerID, r.Amount, r.PaymentDate,
       r.ClientPaymentId, r.EligibleForPostingAtUtc, r.AutoPostEnabled, r.PostedToBoxAtUtc, r.CreatedAtUtc
FROM dbo.CustomersPaymentsRequest r
WHERE r.DelegateID=@ListId
ORDER BY r.CustomersPaymentsRequestID DESC;

PRINT '=== Gap counts ===';
SELECT
  (SELECT COUNT(*) FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId) AS Requests,
  (SELECT COUNT(*) FROM dbo.CustomersPayments WHERE DelegateID=@ListId) AS Payments,
  (SELECT COUNT(*) FROM dbo.CustomersPaymentsRequest r
   WHERE r.DelegateID=@ListId
     AND r.ClientPaymentId IS NOT NULL
     AND NOT EXISTS (
       SELECT 1 FROM dbo.CustomersPayments p WHERE p.ClientPaymentId = r.ClientPaymentId
     )) AS ReqMissingByClientId,
  (SELECT COUNT(*) FROM dbo.CustomersPaymentsRequest r
   WHERE r.DelegateID=@ListId AND r.ClientPaymentId IS NULL) AS ReqNullClientId;

PRINT '=== View remaining sample for due ===';
IF OBJECT_ID('dbo.View_CustomersPaymentsRequestFinal','V') IS NOT NULL
BEGIN
  SELECT TOP 15
    cpr.CustomersPaymentsRequestID, cpr.Amount, v.AmountRemaining,
    CASE WHEN v.AmountRemaining >= cpr.Amount THEN 1 ELSE 0 END AS CanPost,
    cpr.EligibleForPostingAtUtc, cpr.AutoPostEnabled, cpr.PostedToBoxAtUtc
  FROM CustomersPaymentsRequest cpr
  JOIN View_CustomersPaymentsRequestFinal v ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
  WHERE cpr.DelegateID=@ListId
    AND cpr.PostedToBoxAtUtc IS NULL
  ORDER BY cpr.CustomersPaymentsRequestID DESC;
END
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
print('DB', db, 'SERVER', server)
sql=Path('/tmp/wilaya2_probe.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql')
os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin
    break
r=subprocess.run(cmd, capture_output=True, text=True)
out=(r.stdout or '')
print(out if len(out)<50000 else out[-50000:])
if r.stderr:
  print('STDERR', r.stderr[-3000:])
print('exit', r.returncode)
os.unlink(path)
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_probe.sql", "w") as f:
    f.write(SQL)
with sftp.file("/tmp/wilaya2_probe_runner.py", "w") as f:
    f.write(RUNNER)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/wilaya2_probe_runner.py", timeout=180)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR", e[-2000:])
c.close()
