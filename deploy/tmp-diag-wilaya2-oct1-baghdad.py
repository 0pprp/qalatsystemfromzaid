#!/usr/bin/env python3
"""Match Wilaya-2 payments to Baghdad business day 2026-10-01 (03:00→02:59)."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
DECLARE @ListId INT = 5;
-- Baghdad business day 2026-10-01: [03:00 Oct1, 03:00 Oct2) Baghdad = [00:00 Oct1, 00:00 Oct2) UTC
DECLARE @BizStartUtc datetime2 = '2026-10-01T00:00:00';
DECLARE @BizEndUtc datetime2 = '2026-10-02T00:00:00';
-- Baghdad calendar day 2026-10-01: [00:00, 24:00) Baghdad = [2026-09-30 21:00, 2026-10-01 21:00) UTC
DECLARE @CalStartUtc datetime2 = '2026-09-30T21:00:00';
DECLARE @CalEndUtc datetime2 = '2026-10-01T21:00:00';

PRINT '=== Columns on CustomersPayments ===';
SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.CustomersPayments') ORDER BY column_id;

PRINT '=== By PaymentDate as Baghdad calendar (DATEADD hour 3) ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, DATEADD(HOUR,3,p.PaymentDate))='2026-10-01';

PRINT '=== By PaymentDate in business-day UTC window ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND p.PaymentDate >= @BizStartUtc AND p.PaymentDate < @BizEndUtc;

PRINT '=== By PaymentDate in calendar-day UTC window ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND p.PaymentDate >= @CalStartUtc AND p.PaymentDate < @CalEndUtc;

PRINT '=== Raw CONVERT(date,PaymentDate)=Oct1 (server prior) ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-01';

PRINT '=== List all payments PaymentDate Baghdad-cal = Oct1 ===';
SELECT p.CustomerPaymentID, p.CustomerID,
       CONVERT(varchar(19), p.PaymentDate, 120) AS PayUtc,
       CONVERT(varchar(19), DATEADD(HOUR,3,p.PaymentDate), 120) AS PayBaghdad,
       ISNULL(a.Amount,0)*1448 AS Iqd,
       ISNULL(a.Amount,0)*1448/1000.0 AS IqdK,
       LEFT(ISNULL(c.CustomerName,''),40) AS Name,
       LEFT(CONVERT(varchar(64),p.ClientPaymentId),36) AS ClientPaymentId
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, DATEADD(HOUR,3,p.PaymentDate))='2026-10-01'
ORDER BY p.PaymentDate, p.CustomerPaymentID;

PRINT '=== SUM check target 2370 / 34 ===';
SELECT
  (SELECT COUNT(*) FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
   WHERE p.DelegateID=@ListId AND CONVERT(date, DATEADD(HOUR,3,p.PaymentDate))='2026-10-01') AS CntBaghdad,
  (SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0 FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
   WHERE p.DelegateID=@ListId AND CONVERT(date, DATEADD(HOUR,3,p.PaymentDate))='2026-10-01') AS KBaghdad;
"""

REMOTE = r"""
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
sql=Path('/tmp/wilaya2_baghdad.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','160','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-2000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required"); return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_baghdad.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_baghdad_runner.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_baghdad_runner.py", timeout=180)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    path = Path(__file__).with_name("_wilaya2_baghdad_out.txt")
    path.write_text(text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8")
    print(f"wrote {path} size={path.stat().st_size}")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
