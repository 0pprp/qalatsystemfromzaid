#!/usr/bin/env python3
"""Diagnose CustomerID 51962 صباح حسن عودة 8 week amounts."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;

DECLARE @Cid INT = 51962;

PRINT '=== Customer ===';
SELECT CustomerID, CustomerName, AmountRemaining, ReceiptsTotal, LastPaymentDate,
       Amount1, Amount2, Amount3, Amount4, Amount5, Amount6, Amount7
FROM dbo.View_Customers WHERE CustomerID=@Cid;

PRINT '=== ReceiptCustomerDate last 10d ===';
SELECT CustomerPaymentID, AmountDenar, PaymentDate,
       CONVERT(date,PaymentDate) AS D,
       DATEDIFF(DAY, PaymentDate, GETDATE()) AS DaysAgoGetDate,
       DATEDIFF(DAY, PaymentDate, GETUTCDATE()) AS DaysAgoUtc
FROM dbo.View_ReceiptCustomerDate
WHERE CustomerID=@Cid
  AND PaymentDate >= DATEADD(day,-10, GETDATE())
ORDER BY PaymentDate DESC;

PRINT '=== WeekFinal ===';
SELECT * FROM dbo.View_WeekFinal WHERE CustomerID=@Cid;

PRINT '=== PaymentDeviceWeek ===';
SELECT CustomerID, Amount1, Amount2, Amount3, Amount4, Amount5, Amount6, Amount7
FROM dbo.View_PaymentDeviceWeek WHERE CustomerID=@Cid;

PRINT '=== Raw payment 418752 ===';
SELECT p.CustomerPaymentID, p.CustomerID, p.PaymentDate, a.Amount, a.Amount*1448 AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.CustomerPaymentID=418752 OR (p.CustomerID=@Cid AND CONVERT(date,p.PaymentDate)='2026-10-02');

PRINT '=== Definition snippet WeekFinal ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.View_WeekFinal')) AS Def;
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_sabah8.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_sabah8_runner.py", "w") as f:
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
sql=Path('/tmp/wilaya2_sabah8.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i',path], capture_output=True, text=True)
Path('/tmp/wilaya2_sabah8_out.txt').write_text((r.stdout or '')+'\n'+(r.stderr or ''), encoding='utf-8')
print('EXIT', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_sabah8_runner.py", timeout=120)
    print(out.read().decode("utf-8", "replace"))
    _, out2, _ = c.exec_command("cat /tmp/wilaya2_sabah8_out.txt", timeout=30)
    remote = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_sabah8_remote.txt").write_text(remote, encoding="utf-8")
    # print ascii-safe summary
    for line in remote.splitlines():
        safe = line.encode("ascii", "replace").decode("ascii")
        print(safe)
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
