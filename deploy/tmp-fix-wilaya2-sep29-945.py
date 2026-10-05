#!/usr/bin/env python3
"""
Match Wilaya-2 Sep29 to receipt: 39 payments / 945,000 IQD.

Remove two Oct1 leftovers that landed on Sep29 and are not on the receipt:
- 418784 حيدر صاحب عباس 50,000
- 418572 مصطفى صادق كريم 20,000
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
BEGIN TRAN;

DECLARE @ListId INT = 5;
DECLARE @Day date = '2026-09-29';

PRINT '=== BEFORE ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== REMOVE extras not on receipt ===';
SELECT p.CustomerPaymentID, p.CustomerID, LEFT(c.CustomerName,40) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.CustomerPaymentID IN (418784, 418572);

UPDATE dbo.CustomersPayments
SET PaymentDate = '2026-09-26T12:00:00',
    UpdatedDate = GETDATE(),
    Location = N'removed-not-in-sep29-receipt-945'
WHERE CustomerPaymentID IN (418784, 418572)
  AND DelegateID = @ListId
  AND CONVERT(date, PaymentDate) = @Day;
PRINT CONCAT('MovedOff=', @@ROWCOUNT);

PRINT '=== AFTER ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

SELECT p.CustomerPaymentID, p.CustomerID, LEFT(c.CustomerName,40) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY Iqd DESC, p.CustomerPaymentID;

DECLARE @Cnt INT, @Iqd bigint;
SELECT @Cnt=COUNT(*), @Iqd=CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

IF @Cnt = 39 AND @Iqd = 945000
BEGIN
  COMMIT TRAN;
  PRINT CONCAT('COMMITTED_OK cnt=',@Cnt,' iqd=',@Iqd);
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT CONCAT('ROLLBACK cnt=',@Cnt,' iqd=',@Iqd,' expected=39/945000');
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
sql=Path('/tmp/wilaya2_sep29_fix.sql').read_text(encoding='utf-8-sig')
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
    with sftp.file("/tmp/wilaya2_sep29_fix.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_sep29_fix_runner.py", "w") as f:
        f.write(runner)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_sep29_fix_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_sep29_fix_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1000:])
    c.close()
    return 0 if "COMMITTED_OK" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
