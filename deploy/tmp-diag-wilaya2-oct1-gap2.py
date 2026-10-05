#!/usr/bin/env python3
"""Deeper gap hunt: missing 515K between claimed 2370 and posted 1855 for Wilaya-2 on 2026-10-01."""
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
DECLARE @ListId INT = 5;
DECLARE @DayStart datetime2 = CAST(@Day AS datetime2);
DECLARE @DayEnd datetime2 = DATEADD(day,1,@DayStart);

PRINT '=== 1) Payments on list5 customers but payment.DelegateID != 5 (Oct1) ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE c.DelegateID=@ListId AND p.DelegateID<>@ListId
  AND CONVERT(date,p.PaymentDate)=@Day;

SELECT TOP 30 p.CustomerPaymentID, p.CustomerID, p.DelegateID AS PayList,
       c.DelegateID AS CustList, p.PaymentDate, p.ClientPaymentId,
       ISNULL(a.Amount,0)*1448/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE c.DelegateID=@ListId AND p.DelegateID<>@ListId
  AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY a.Amount DESC;

PRINT '=== 2) AddToBox CreatedDate on Oct1 for list5 (vs PaymentDate) ===';
SELECT
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)=@Day THEN 1 ELSE 0 END) AS ByPayDateCnt,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)=@Day THEN ISNULL(a.Amount,0)*1448 ELSE 0 END)/1000.0 AS ByPayDateK,
  SUM(CASE WHEN CONVERT(date,a.CreatedDate)=@Day OR CONVERT(date,a.DateCreate)=@Day THEN 1 ELSE 0 END) AS ByBoxCreateCnt,
  SUM(CASE WHEN CONVERT(date,a.CreatedDate)=@Day OR CONVERT(date,a.DateCreate)=@Day THEN ISNULL(a.Amount,0)*1448 ELSE 0 END)/1000.0 AS ByBoxCreateK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND (CONVERT(date,p.PaymentDate)=@Day OR CONVERT(date,a.CreatedDate)=@Day OR CONVERT(date,a.DateCreate)=@Day);

PRINT '=== 3) All request rows ever for list5 around Oct1 (CreatedAtUtc Iraq) ===';
SELECT COUNT(*) AS ReqNear,
       SUM(ISNULL(Amount,0))/1000.0 AS AmtK,
       MIN(CreatedAtUtc) AS MinUtc, MAX(CreatedAtUtc) AS MaxUtc
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND CreatedAtUtc >= DATEADD(HOUR,-3,@DayStart)
  AND CreatedAtUtc < DATEADD(HOUR,21,@DayEnd);

SELECT TOP 20 CustomersPaymentsRequestID, CustomerID, Amount/1000.0 AS AmtK, PaymentDate,
       ClientPaymentId, PostedToBoxAtUtc, CreatedAtUtc, EligibleForPostingAtUtc, AutoPostEnabled
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND CreatedAtUtc >= DATEADD(HOUR,-3,@DayStart)
  AND CreatedAtUtc < DATEADD(HOUR,21,@DayEnd)
ORDER BY CreatedAtUtc DESC;

PRINT '=== 4) Request table last activity list5 ===';
SELECT TOP 10 CustomersPaymentsRequestID, Amount/1000.0 AS AmtK, PaymentDate, CreatedAtUtc, PostedToBoxAtUtc, ClientPaymentId
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId
ORDER BY CustomersPaymentsRequestID DESC;

PRINT '=== 5) Full payment list Oct1 with running sum ===';
;WITH x AS (
  SELECT p.CustomerPaymentID, p.CustomerID, p.PaymentDate, p.ClientPaymentId,
         ISNULL(a.Amount,0)*1448/1000.0 AS IqdK,
         SUM(ISNULL(a.Amount,0)*1448/1000.0) OVER (ORDER BY p.PaymentDate, p.CustomerPaymentID) AS RunK
  FROM dbo.CustomersPayments p
  LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
)
SELECT * FROM x ORDER BY PaymentDate, CustomerPaymentID;

PRINT '=== 6) DaySales / AmountDaySales style on customers of list5 ===';
IF COL_LENGTH('dbo.View_Customers','AmountDaySales') IS NOT NULL
BEGIN
  SELECT COUNT(*) AS CustWithDaySales,
         SUM(ISNULL(AmountDaySales,0))/1000.0 AS DaySalesK,
         SUM(ISNULL(AmountTotalSales,0))/1000.0 AS TotalSalesK,
         SUM(ISNULL(AmountRemaining,0))/1000.0 AS RemainingK
  FROM dbo.View_Customers WHERE DelegateID=@ListId AND ISNULL(AmountDaySales,0)>0;
END

PRINT '=== 7) Payments Oct1 null ClientPaymentId ===';
SELECT COUNT(*) AS NullIdCnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day AND p.ClientPaymentId IS NULL;

PRINT '=== 8) Soft-delete / IsDeleted columns? ===';
SELECT t.name AS Tbl, c.name AS Col
FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
WHERE t.name IN ('CustomersPayments','CustomersPaymentsRequest','AddToBox')
  AND (c.name LIKE '%Delete%' OR c.name LIKE '%Void%' OR c.name LIKE '%Cancel%' OR c.name LIKE '%Active%');

PRINT '=== 9) Compare Oct1 vs Sep30 / Oct2 for list5 ===';
SELECT CONVERT(date,p.PaymentDate) AS D, COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate) BETWEEN '2026-09-28' AND '2026-10-03'
GROUP BY CONVERT(date,p.PaymentDate)
ORDER BY D;

PRINT '=== 10) Any payment with Notes/Document hint 2370 ===';
SELECT TOP 5 name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.CustomersPayments');

PRINT '=== 11) Follower collection payments Oct1 list5? ===';
IF OBJECT_ID('dbo.CustomersPaymentsRequest','U') IS NOT NULL
  AND COL_LENGTH('dbo.CustomersPaymentsRequest','Source') IS NOT NULL
  SELECT Source, COUNT(*) FROM dbo.CustomersPaymentsRequest GROUP BY Source;

-- payments created by non-delegate users?
IF COL_LENGTH('dbo.CustomersPayments','UserID') IS NOT NULL
  SELECT TOP 20 p.CustomerPaymentID, p.UserID, ISNULL(a.Amount,0)*1448/1000.0 AS IqdK
  FROM dbo.CustomersPayments p
  LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== 12) Tables that might hold offline queue archive ===';
SELECT name FROM sys.tables
WHERE name LIKE '%Payment%' OR name LIKE '%Sync%' OR name LIKE '%Outbox%' OR name LIKE '%Queue%'
ORDER BY name;
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
print('DB', db)
sql=Path('/tmp/wilaya2_oct1_gap2.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['/opt/mssql-tools18/bin/sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','100','-s','|','-i',path]
if not os.path.isfile(cmd[0]):
  cmd[0]='sqlcmd'
r=subprocess.run(cmd, capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-3000:])
print('exit', r.returncode)
os.unlink(path)
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required"); return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_oct1_gap2.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_oct1_gap2_runner.py", "w") as f:
        f.write(RUNNER)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_oct1_gap2_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    path = Path(__file__).with_name("_wilaya2_oct1_gap2_out.txt")
    path.write_text(text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8")
    print(f"wrote {path} size={path.stat().st_size}")
    # also pull delegate logs mentioning payment fail around Oct1
    _, out2, _ = c.exec_command(
        "ls -1 /opt/qalaa-runtime | grep -i 'delegate.*najaf' ; "
        "journalctl -u 'qalaa-delegate*' --since '2026-10-01' --until '2026-10-02' --no-pager 2>/dev/null | "
        "grep -iE 'payment|تسديد|wilaya|ولاية|ClientPayment|duplicate|fail|error' | tail -n 80",
        timeout=60,
    )
    logs = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_oct1_logs.txt").write_text(logs, encoding="utf-8")
    print("logs_bytes", len(logs))
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
