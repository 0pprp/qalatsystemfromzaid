#!/usr/bin/env python3
"""Diagnose Wilaya-2 Oct2: صباح حسن عودة receipt Amount1-7 vs payments."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;

PRINT CONCAT('ServerNow=', CONVERT(varchar(30), GETDATE(), 120),
  ' Utc=', CONVERT(varchar(30), GETUTCDATE(), 120),
  ' UtcDate=', CONVERT(varchar(10), CONVERT(date, GETUTCDATE()), 120),
  ' UtcYday=', CONVERT(varchar(10), CONVERT(date, GETUTCDATE()-1), 120),
  ' LocalYday=', CONVERT(varchar(10), CONVERT(date, GETDATE()-1), 120));

DECLARE @ListId INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE DelegateName LIKE N'%ولاية%2%' OR DelegateName LIKE N'%ولاية نجف 2%' OR DelegateName = N'الولاية نجف 2'
  ORDER BY CASE WHEN DelegateName LIKE N'%2%' THEN 0 ELSE 1 END, DelegateID
);
IF @ListId IS NULL SET @ListId = 5;
DECLARE @ListName nvarchar(200) = (SELECT DelegateName FROM dbo.Delegates WHERE DelegateID=@ListId);
PRINT CONCAT('ListId=', @ListId, ' Name=', @ListName);

PRINT '=== CUSTOMERS matching صباح حسن عودة ===';
SELECT c.CustomerID, c.CustomerName, c.DelegateID, d.DelegateName,
       vc.AmountRemaining, vc.ReceiptsTotal, vc.AmountTotalSales, vc.LastPaymentDate
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID=c.CustomerID
WHERE c.CustomerName LIKE N'%صباح%حسن%عودة%'
   OR c.CustomerName LIKE N'%صباح حسن عودة%';

DECLARE @Cid INT = (
  SELECT TOP 1 c.CustomerID FROM dbo.Customers c
  WHERE (c.CustomerName LIKE N'%صباح%حسن%عودة%' OR c.CustomerName LIKE N'%صباح حسن عودة%')
    AND (c.DelegateID=@ListId OR EXISTS (
      SELECT 1 FROM dbo.CustomersPayments p
      WHERE p.CustomerID=c.CustomerID AND p.DelegateID=@ListId
        AND CONVERT(date,p.PaymentDate) IN ('2026-10-01','2026-10-02','2026-10-03')))
  ORDER BY CASE WHEN c.DelegateID=@ListId THEN 0 ELSE 1 END, c.CustomerID
);
PRINT CONCAT('PickCid=', @Cid);

PRINT '=== PAYMENTS last 10 days for customer ===';
SELECT p.CustomerPaymentID, p.CustomerID, p.DelegateID,
       p.PaymentDate, CONVERT(date,p.PaymentDate) AS PayDate,
       p.CreatedDate, p.ClientPaymentId,
       a.Amount AS BoxUsd, ISNULL(a.Amount,0)*1448 AS BoxIqd,
       a.Notes
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.CustomerID=@Cid
  AND CONVERT(date,p.PaymentDate) >= DATEADD(day,-10, CONVERT(date, GETUTCDATE()))
ORDER BY p.PaymentDate DESC, p.CustomerPaymentID DESC;

PRINT '=== REQUESTS last 10 days ===';
SELECT r.CustomersPaymentsRequestID, r.CustomerID, r.DelegateID, r.Amount,
       r.PaymentDate, CONVERT(date,r.PaymentDate) AS PayDate,
       r.CreatedAtUtc, r.PostedToBoxAtUtc, r.ClientPaymentId
FROM dbo.CustomersPaymentsRequest r
WHERE r.CustomerID=@Cid
  AND (
    CONVERT(date,r.PaymentDate) >= DATEADD(day,-10, CONVERT(date, GETUTCDATE()))
    OR CONVERT(date, DATEADD(HOUR,3,r.CreatedAtUtc)) >= DATEADD(day,-10, CONVERT(date, GETUTCDATE()))
  )
ORDER BY r.PaymentDate DESC;

PRINT '=== View_CustomersPayments rows ===';
SELECT TOP 20 CustomerPaymentID, CustomerID, DelegateID, PaymentDate,
       CONVERT(date,PaymentDate) AS PayDate, AmountDenar
FROM dbo.View_CustomersPayments
WHERE CustomerID=@Cid
ORDER BY PaymentDate DESC;

PRINT '=== Week Amounts from View_PaymentDeviceWeek ===';
IF OBJECT_ID('dbo.View_PaymentDeviceWeek','V') IS NOT NULL
  SELECT CustomerID, CustomerName, AmountRemaining, ReceiptsTotal,
         Amount1, Amount2, Amount3, Amount4, Amount5, Amount6, Amount7
  FROM dbo.View_PaymentDeviceWeek WHERE CustomerID=@Cid;

PRINT '=== Week from View_CustomersSalesAndPaymentWeekDevice if exists ===';
IF OBJECT_ID('dbo.View_CustomersSalesAndPaymentWeekDevice','V') IS NOT NULL
  SELECT TOP 1 CustomerID, CustomerName, AmountRemaining,
         Amount1, Amount2, Amount3, Amount4, Amount5, Amount6, Amount7
  FROM dbo.View_CustomersSalesAndPaymentWeekDevice WHERE CustomerID=@Cid;

PRINT '=== Compare PaymentDate equality styles for Oct2 ===';
SELECT
  SUM(CASE WHEN PaymentDate = CONVERT(date,'2026-10-02') THEN AmountDenar ELSE 0 END) AS EqDateOnly,
  SUM(CASE WHEN CONVERT(date,PaymentDate)='2026-10-02' THEN AmountDenar ELSE 0 END) AS ConvertDate,
  SUM(CASE WHEN PaymentDate >= '2026-10-02' AND PaymentDate < '2026-10-03' THEN AmountDenar ELSE 0 END) AS RangeDay
FROM dbo.View_CustomersPayments WHERE CustomerID=@Cid;

PRINT '=== Wilaya2 Oct2: payments where CONVERT date matches but equality to date fails ===';
SELECT COUNT(*) AS BadEqCnt,
       SUM(ISNULL(a.Amount,0)*1448) AS BadEqIqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-10-02'
  AND NOT (p.PaymentDate = CONVERT(date,'2026-10-02'));

PRINT '=== Sample bad-eq payments Oct2 ===';
SELECT TOP 30 p.CustomerPaymentID, c.CustomerName, p.PaymentDate,
       CONVERT(date,p.PaymentDate) AS D, ISNULL(a.Amount,0)*1448 AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-10-02'
  AND NOT (p.PaymentDate = CONVERT(date,'2026-10-02'))
ORDER BY c.CustomerName;

PRINT '=== Oct2 list totals ===';
SELECT COUNT(*) AS PayCnt, SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02';
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_oct2_sabah.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_oct2_sabah_runner.py", "w") as f:
        f.write(
            r"""
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
sql=Path('/tmp/wilaya2_oct2_sabah.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not Path(bin).exists():
    bin='/opt/mssql-tools/bin/sqlcmd'
cmd=[bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','80','-s','|','-i',path]
print('RUN', server, db)
r=subprocess.run(cmd, capture_output=True, text=True)
Path('/tmp/wilaya2_oct2_sabah_out.txt').write_text((r.stdout or '')+'\n'+(r.stderr or ''), encoding='utf-8')
print(r.stdout or '')
if (r.stderr or '').strip():
    print('STDERR', (r.stderr or '')[-2000:])
print('EXIT', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_oct2_sabah_runner.py", timeout=180)
    text = out.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_oct2_sabah_out.txt").write_text(text, encoding="utf-8")
    _, out2, _ = c.exec_command("cat /tmp/wilaya2_oct2_sabah_out.txt", timeout=30)
    remote = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_oct2_sabah_remote.txt").write_text(remote, encoding="utf-8")
    print(remote)
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("SSHERR", e[-1500:])
    c.close()
    return 0 if "PickCid=" in remote else 1


if __name__ == "__main__":
    raise SystemExit(main())
