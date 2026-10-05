#!/usr/bin/env python3
"""
Remove 70k excess from Wilaya-2 box by deleting the two phantom payments:
- 418572 مصطفى صادق كريم 20,000
- 418784 حيدر صاحب عباس 50,000 (was inserted for Oct1 match, then removed from days)
Uses dbo.CustomersPayments_Delete which also deletes AddToBox.
"""
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
SET XACT_ABORT ON;

DECLARE @BoxID INT = 6;
DECLARE @UserDeleteID INT = (
  SELECT TOP 1 UserID FROM dbo.CustomersPayments
  WHERE CustomerPaymentID IN (418572, 418784) AND UserID IS NOT NULL
  ORDER BY CustomerPaymentID
);
IF @UserDeleteID IS NULL
  SET @UserDeleteID = (SELECT TOP 1 UserID FROM dbo.Users WHERE UserState=1 ORDER BY UserID);

PRINT CONCAT('UserDeleteID=', @UserDeleteID);

PRINT '=== BEFORE View_Box ===';
IF OBJECT_ID('dbo.View_Box','V') IS NOT NULL
  SELECT * FROM dbo.View_Box WHERE BoxID=@BoxID;

PRINT '=== BEFORE AddToBox sum ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.AddToBox WHERE BoxID=@BoxID;

PRINT '=== Targets ===';
SELECT p.CustomerPaymentID, a.AddToBoxID, LEFT(c.CustomerName,40) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd,
       CONVERT(varchar(19),p.PaymentDate,120) AS PayD
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.CustomerPaymentID IN (418572, 418784);

BEGIN TRAN;

EXEC dbo.CustomersPayments_Delete @CustomerPaymentID=418572, @UserDeleteID=@UserDeleteID;
EXEC dbo.CustomersPayments_Delete @CustomerPaymentID=418784, @UserDeleteID=@UserDeleteID;

PRINT '=== AFTER delete existence ===';
SELECT
  (SELECT COUNT(*) FROM dbo.CustomersPayments WHERE CustomerPaymentID IN (418572,418784)) AS PayLeft,
  (SELECT COUNT(*) FROM dbo.AddToBox WHERE CustomerPaymentID IN (418572,418784)) AS BoxLeft;

PRINT '=== AFTER AddToBox sum ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.AddToBox WHERE BoxID=@BoxID;

PRINT '=== AFTER View_Box ===';
IF OBJECT_ID('dbo.View_Box','V') IS NOT NULL
  SELECT * FROM dbo.View_Box WHERE BoxID=@BoxID;

-- Keep Sep29 / Oct1 intact
DECLARE @Sep29Cnt INT, @Sep29Iqd bigint, @Oct1Cnt INT, @Oct1Iqd bigint;
SELECT @Sep29Cnt=COUNT(*), @Sep29Iqd=CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=5 AND CONVERT(date,p.PaymentDate)='2026-09-29';
SELECT @Oct1Cnt=COUNT(*), @Oct1Iqd=CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=5 AND CONVERT(date,p.PaymentDate)='2026-10-01';

PRINT CONCAT('Sep29=',@Sep29Cnt,'/',@Sep29Iqd,' Oct1=',@Oct1Cnt,'/',@Oct1Iqd);

IF @Sep29Cnt=39 AND @Sep29Iqd=945000 AND @Oct1Cnt=36 AND @Oct1Iqd=2370000
  AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments WHERE CustomerPaymentID IN (418572,418784))
  AND NOT EXISTS (SELECT 1 FROM dbo.AddToBox WHERE CustomerPaymentID IN (418572,418784))
BEGIN
  COMMIT TRAN;
  PRINT 'COMMITTED_BOX_MINUS_70K';
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT 'ROLLBACK';
END
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
sql=Path('/tmp/wilaya2_box70_fix.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-4000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_box70_fix.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_box70_fix_runner.py", "w") as f:
        f.write(runner)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_box70_fix_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_box70_fix_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1500:])
    c.close()
    return 0 if "COMMITTED_BOX_MINUS_70K" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
