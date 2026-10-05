#!/usr/bin/env python3
"""Debug Wilaya-2 (Najaf) payments for 2026-10-01: gap, dupes, under-count."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;

DECLARE @Day date = '2026-10-01';
DECLARE @ListId INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE DelegateName LIKE N'%ولاية%2%' OR DelegateName LIKE N'%ولاية نجف 2%' OR DelegateName = N'الولاية نجف 2'
  ORDER BY CASE WHEN DelegateName LIKE N'%2%' THEN 0 ELSE 1 END, DelegateID
);
IF @ListId IS NULL SET @ListId = 5;

PRINT '=== LIST ===';
SELECT @ListId AS ListId, (SELECT DelegateName FROM dbo.Delegates WHERE DelegateID=@ListId) AS Name, @Day AS Day;

PRINT '=== SCHEMA: amount paths ===';
SELECT 'CustomersPayments' AS T, c.name
FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.CustomersPayments')
  AND (c.name LIKE '%Amount%' OR c.name IN ('ClientPaymentId','PaymentDate','DelegateID','CustomerID','CustomerPaymentID','IsDeleted','DeletedAtUtc'))
UNION ALL
SELECT 'CustomersPaymentsRequest', c.name
FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.CustomersPaymentsRequest')
  AND (c.name LIKE '%Amount%' OR c.name IN ('ClientPaymentId','PaymentDate','DelegateID','CustomerID','PostedToBoxAtUtc','EligibleForPostingAtUtc','AutoPostEnabled','CreatedAtUtc','IsDeleted'))
UNION ALL
SELECT 'AddToBox', c.name
FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.AddToBox')
ORDER BY 1,2;

PRINT '=== DAY SALES / وارد views if exist ===';
IF OBJECT_ID('dbo.View_Customers','V') IS NOT NULL
  SELECT 'View_Customers day fields' AS Info;
SELECT TOP 1 name FROM sys.views WHERE name LIKE '%Day%' OR name LIKE '%Receipt%' ORDER BY name;

PRINT '=== REQUESTS on day (PaymentDate / CreatedAt) ===';
SELECT
  COUNT(*) AS ReqCnt,
  SUM(ISNULL(Amount,0)) AS ReqAmtRaw,
  SUM(ISNULL(Amount,0))/1000.0 AS ReqAmtK,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPosted,
  SUM(CASE WHEN PostedToBoxAtUtc IS NOT NULL THEN 1 ELSE 0 END) AS PostedFlag,
  SUM(CASE WHEN ClientPaymentId IS NULL THEN 1 ELSE 0 END) AS NullClientId
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND (
    CONVERT(date, PaymentDate)=@Day
    OR CONVERT(date, DATEADD(HOUR,3,CreatedAtUtc))=@Day
  );

PRINT '=== REQUESTS by PaymentDate only ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt, SUM(ISNULL(Amount,0))/1000.0 AS AmtK
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId AND CONVERT(date, PaymentDate)=@Day;

PRINT '=== PAYMENTS on day ===';
SELECT
  COUNT(*) AS PayCnt,
  SUM(ISNULL(a.Amount,0)) AS BoxUsd,
  SUM(ISNULL(a.Amount,0)*1448) AS BoxIqd,
  SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS BoxIqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date, p.PaymentDate)=@Day;

PRINT '=== View_ReceiptCustomerDate day sum (if exists) ===';
IF OBJECT_ID('dbo.View_ReceiptCustomerDate','V') IS NOT NULL
BEGIN
  SELECT COUNT(*) AS RcptRows,
         SUM(ISNULL(AmountDenar,0)) AS SumDenar,
         SUM(ISNULL(AmountDenar,0))/1000.0 AS SumDenarK
  FROM dbo.View_ReceiptCustomerDate v
  JOIN dbo.Customers c ON c.CustomerID=v.CustomerID
  WHERE c.DelegateID=@ListId AND CONVERT(date, v.PaymentDate)=@Day;
END

PRINT '=== DUPLICATE ClientPaymentId in REQUESTS day ===';
SELECT ClientPaymentId, COUNT(*) AS Cnt, SUM(ISNULL(Amount,0)) AS Amt
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId AND CONVERT(date, PaymentDate)=@Day
  AND ClientPaymentId IS NOT NULL
GROUP BY ClientPaymentId
HAVING COUNT(*) > 1
ORDER BY Cnt DESC;

PRINT '=== DUPLICATE ClientPaymentId in PAYMENTS day ===';
SELECT p.ClientPaymentId, COUNT(*) AS Cnt,
       SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date, p.PaymentDate)=@Day
  AND p.ClientPaymentId IS NOT NULL
GROUP BY p.ClientPaymentId
HAVING COUNT(*) > 1
ORDER BY Cnt DESC;

PRINT '=== Same CustomerID+Amount+PaymentDate multiples (PAY) ===';
SELECT p.CustomerID, CONVERT(date,p.PaymentDate) AS D, COUNT(*) AS Cnt,
       SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date, p.PaymentDate)=@Day
GROUP BY p.CustomerID, CONVERT(date,p.PaymentDate)
HAVING COUNT(*) > 1
ORDER BY Cnt DESC;

PRINT '=== Requests WITHOUT matching payment by ClientPaymentId ===';
SELECT COUNT(*) AS MissingCnt, SUM(ISNULL(r.Amount,0)) AS MissingAmt,
       SUM(ISNULL(r.Amount,0))/1000.0 AS MissingAmtK
FROM dbo.CustomersPaymentsRequest r
WHERE r.DelegateID=@ListId AND CONVERT(date, r.PaymentDate)=@Day
  AND r.ClientPaymentId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments p WHERE p.ClientPaymentId=r.ClientPaymentId);

PRINT '=== Missing request sample ===';
SELECT TOP 40 r.CustomersPaymentsRequestID, r.CustomerID, r.Amount, r.Amount/1000.0 AS AmtK,
       r.PaymentDate, r.ClientPaymentId, r.EligibleForPostingAtUtc, r.AutoPostEnabled,
       r.PostedToBoxAtUtc, r.CreatedAtUtc
FROM dbo.CustomersPaymentsRequest r
WHERE r.DelegateID=@ListId AND CONVERT(date, r.PaymentDate)=@Day
  AND r.ClientPaymentId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments p WHERE p.ClientPaymentId=r.ClientPaymentId)
ORDER BY r.Amount DESC;

PRINT '=== Payments WITHOUT matching request ClientPaymentId ===';
SELECT COUNT(*) AS OrphanPayCnt,
       SUM(ISNULL(a.Amount,0)*1448) AS OrphanIqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date, p.PaymentDate)=@Day
  AND p.ClientPaymentId IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPaymentsRequest r WHERE r.ClientPaymentId=p.ClientPaymentId
  );

PRINT '=== Amount mismatch: request vs AddToBox for matched ClientPaymentId ===';
SELECT TOP 40
  r.CustomersPaymentsRequestID, r.CustomerID, r.Amount AS ReqAmt, r.Amount/1000.0 AS ReqK,
  ISNULL(a.Amount,0)*1448 AS PayIqd, (ISNULL(a.Amount,0)*1448)/1000.0 AS PayK,
  (r.Amount - ISNULL(a.Amount,0)*1448) AS DiffIqd,
  r.ClientPaymentId, p.CustomerPaymentID
FROM dbo.CustomersPaymentsRequest r
JOIN dbo.CustomersPayments p ON p.ClientPaymentId=r.ClientPaymentId
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE r.DelegateID=@ListId AND CONVERT(date, r.PaymentDate)=@Day
  AND ABS(r.Amount - ISNULL(a.Amount,0)*1448) > 1
ORDER BY ABS(r.Amount - ISNULL(a.Amount,0)*1448) DESC;

PRINT '=== Eligibility blockers for day requests not posted ===';
IF OBJECT_ID('dbo.View_CustomersPaymentsRequestFinal','V') IS NOT NULL
BEGIN
  SELECT
    COUNT(*) AS Pending,
    SUM(CASE WHEN v.AmountRemaining IS NULL OR v.AmountRemaining <= 0 THEN 1 ELSE 0 END) AS NoRemaining,
    SUM(CASE WHEN v.AmountRemaining > 0 AND v.AmountRemaining < cpr.Amount THEN 1 ELSE 0 END) AS Insufficient,
    SUM(CASE WHEN v.AmountRemaining >= cpr.Amount THEN 1 ELSE 0 END) AS CanPost,
    SUM(CASE WHEN cpr.EligibleForPostingAtUtc IS NULL THEN 1 ELSE 0 END) AS NullEligible,
    SUM(CASE WHEN cpr.EligibleForPostingAtUtc > SYSUTCDATETIME() THEN 1 ELSE 0 END) AS FutureEligible,
    SUM(CASE WHEN ISNULL(cpr.AutoPostEnabled,0)=0 THEN 1 ELSE 0 END) AS AutoOff,
    SUM(ISNULL(cpr.Amount,0)) AS PendingAmt,
    SUM(ISNULL(cpr.Amount,0))/1000.0 AS PendingAmtK
  FROM dbo.CustomersPaymentsRequest cpr
  LEFT JOIN dbo.View_CustomersPaymentsRequestFinal v
    ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
  WHERE cpr.DelegateID=@ListId
    AND CONVERT(date, cpr.PaymentDate)=@Day
    AND cpr.PostedToBoxAtUtc IS NULL;
END

PRINT '=== Day totals side-by-side ===';
SELECT 'REQ_PAYDATE' AS Src, COUNT(*) AS Cnt, SUM(ISNULL(Amount,0))/1000.0 AS AmtK
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day
UNION ALL
SELECT 'PAY_BOX', COUNT(*), SUM(ISNULL(a.Amount,0)*1448)/1000.0
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
UNION ALL
SELECT 'REQ_NOT_POSTED', COUNT(*), SUM(ISNULL(Amount,0))/1000.0
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day AND PostedToBoxAtUtc IS NULL
UNION ALL
SELECT 'REQ_POSTED_FLAG', COUNT(*), SUM(ISNULL(Amount,0))/1000.0
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day AND PostedToBoxAtUtc IS NOT NULL;

PRINT '=== Hunt totals near 2370K ===';
;WITH days AS (
  SELECT CONVERT(date, PaymentDate) AS D,
         COUNT(*) AS ReqCnt, SUM(ISNULL(Amount,0))/1000.0 AS ReqK
  FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId
  GROUP BY CONVERT(date, PaymentDate)
)
SELECT TOP 15 D, ReqCnt, ReqK, ABS(ReqK-2370) AS Dist
FROM days ORDER BY ABS(ReqK-2370);

;WITH days2 AS (
  SELECT CONVERT(date, p.PaymentDate) AS D,
         COUNT(*) AS PayCnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS PayK
  FROM dbo.CustomersPayments p
  LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId
  GROUP BY CONVERT(date, p.PaymentDate)
)
SELECT TOP 15 D, PayCnt, PayK, ABS(PayK-2370) AS Dist
FROM days2 ORDER BY ABS(PayK-2370);

PRINT '=== Sample posted payments day (top by amount) ===';
SELECT TOP 25 p.CustomerPaymentID, p.CustomerID, p.PaymentDate, p.ClientPaymentId,
       ISNULL(a.Amount,0)*1448 AS Iqd, (ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date, p.PaymentDate)=@Day
ORDER BY ISNULL(a.Amount,0) DESC;

PRINT '=== Double AddToBox rows per payment? ===';
SELECT a.CustomerPaymentID, COUNT(*) AS BoxRows, SUM(a.Amount) AS UsdSum
FROM dbo.AddToBox a
JOIN dbo.CustomersPayments p ON p.CustomerPaymentID=a.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date, p.PaymentDate)=@Day
GROUP BY a.CustomerPaymentID
HAVING COUNT(*) > 1;
"""

RUNNER = r"""
import json, os, subprocess, tempfile
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
sql=Path('/tmp/wilaya2_oct1.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql')
os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','80','-s','|','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin
    break
r=subprocess.run(cmd, capture_output=True, text=True)
out=(r.stdout or '')
print(out if len(out)<120000 else out[-120000:])
if r.stderr:
  print('STDERR', r.stderr[-4000:])
print('exit', r.returncode)
os.unlink(path)
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_oct1.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_oct1_runner.py", "w") as f:
        f.write(RUNNER)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_oct1_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    out_path = Path(__file__).with_name("_wilaya2_oct1_out.txt")
    out_path.write_text(text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8")
    print(f"wrote {out_path} bytes={out_path.stat().st_size} exit_pending")
    code = out.channel.recv_exit_status()
    c.close()
    # ASCII summary only for console
    for line in text.splitlines():
        if any(k in line for k in ("===", "ListId", "Req", "Pay", "Missing", "Cnt", "Amt", "DB ", "exit", "Box", "Dist", "Pending", "DUP", "Orphan", "CanPost", "hunt", "2370", "DAY", "LIST", "SCHEMA", "Sample", "Double")):
            try:
                print(line)
            except UnicodeEncodeError:
                print(line.encode("ascii", "replace").decode("ascii"))
    print("exit", code)
    return code


if __name__ == "__main__":
    raise SystemExit(main())
