#!/usr/bin/env python3
"""Probe Wilaya-2 box / AddToBox around the 70k extras moved off Sep29."""
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
DECLARE @ListId INT = 5;

PRINT '=== Delegate / Box ===';
SELECT d.DelegateID, d.DelegateName, d.BoxID
FROM dbo.Delegates d WHERE d.DelegateID=@ListId;

PRINT '=== Box balances if columns exist ===';
SELECT TOP 5 b.BoxID, b.BoxName
FROM dbo.Boxes b
WHERE b.BoxID IN (SELECT BoxID FROM dbo.Delegates WHERE DelegateID=@ListId)
   OR b.BoxID IN (
     SELECT TOP 1 BoxID FROM dbo.CustomersPayments WHERE DelegateID=@ListId ORDER BY CustomerPaymentID DESC
   );

PRINT '=== The two moved 70k payments ===';
SELECT p.CustomerPaymentID, p.CustomerID, LEFT(c.CustomerName,40) AS Name,
       CONVERT(varchar(19),p.PaymentDate,120) AS PayD,
       LEFT(ISNULL(p.Location,''),50) AS Loc,
       p.BoxID,
       a.AddToBoxID, CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd,
       CONVERT(varchar(19),ISNULL(a.CreatedDate,a.DateCreate),120) AS BoxCreated,
       LEFT(ISNULL(a.Notes,''),40) AS Notes
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.CustomerPaymentID IN (418784, 418572);

PRINT '=== Sep26 for list5 (where we parked them) ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-26';

PRINT '=== Sep29 / Oct1 check ===';
SELECT CONVERT(date,p.PaymentDate) AS D, COUNT(*) AS Cnt,
       CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate) IN ('2026-09-26','2026-09-29','2026-10-01')
GROUP BY CONVERT(date,p.PaymentDate)
ORDER BY D;

PRINT '=== AddToBox for list5 BoxID last 10 days sum ===';
DECLARE @BoxID INT = (
  SELECT TOP 1 BoxID FROM dbo.CustomersPayments WHERE DelegateID=@ListId ORDER BY CustomerPaymentID DESC
);
SELECT @BoxID AS BoxID;
SELECT CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate)) AS D,
       COUNT(*) AS Cnt,
       CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.AddToBox a
WHERE a.BoxID=@BoxID
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate)) BETWEEN '2026-09-26' AND '2026-10-04'
GROUP BY CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))
ORDER BY D;

PRINT '=== View_WeekFinal / receipt-like if exists for list ===';
IF OBJECT_ID('dbo.View_CustomersPayments','V') IS NOT NULL
BEGIN
  SELECT CONVERT(date,PaymentDate) AS D, COUNT(*) AS Cnt,
         CAST(SUM(ISNULL(AmountDenar,0)) AS bigint) AS Iqd
  FROM dbo.View_CustomersPayments
  WHERE DelegateID=@ListId
    AND CONVERT(date,PaymentDate) IN ('2026-09-26','2026-09-29','2026-10-01')
  GROUP BY CONVERT(date,PaymentDate)
  ORDER BY D;
END

PRINT '=== Any soft-delete columns on AddToBox / payments ===';
SELECT c.name FROM sys.columns c
WHERE c.object_id=OBJECT_ID('dbo.AddToBox')
  AND (c.name LIKE '%Delete%' OR c.name LIKE '%Cancel%' OR c.name LIKE '%Void%' OR c.name IN ('Amount','Notes','BoxID'));
SELECT c.name FROM sys.columns c
WHERE c.object_id=OBJECT_ID('dbo.CustomersPayments')
  AND (c.name LIKE '%Delete%' OR c.name LIKE '%Cancel%' OR c.name IN ('PaymentDate','Location','BoxID'));
"""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    runner = r"""
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
sql=Path('/tmp/wilaya2_box70.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-3000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_box70.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_box70_runner.py", "w") as f:
        f.write(runner)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_box70_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_box70_probe.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
