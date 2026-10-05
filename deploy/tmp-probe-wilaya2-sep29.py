#!/usr/bin/env python3
"""Probe Wilaya-2 (list 5) payments on 2026-09-29 vs receipt target 945k / 39."""
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
DECLARE @Day date = '2026-09-29';

PRINT '=== CURRENT Sep29 ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== ROWS ===';
SELECT p.CustomerPaymentID, p.CustomerID,
       LEFT(ISNULL(c.CustomerName,N''),45) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd,
       CONVERT(varchar(19),p.PaymentDate,120) AS PayD,
       LEFT(ISNULL(p.Location,N''),40) AS Loc
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY Iqd DESC, c.CustomerName;

PRINT '=== DUPLICATE names same day ===';
SELECT LEFT(c.CustomerName,45) AS Name, COUNT(*) AS Cnt,
       CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
GROUP BY c.CustomerName
HAVING COUNT(*)>1
ORDER BY Cnt DESC;

PRINT '=== Oct1 rows we previously moved off Sep29? ===';
SELECT COUNT(*) AS RemovedMarked
FROM dbo.CustomersPayments
WHERE DelegateID=@ListId AND Location LIKE N'%removed-not-in-correct-sheet%';

PRINT '=== Nearby days ===';
SELECT CONVERT(date,p.PaymentDate) AS D, COUNT(*) AS Cnt,
       CAST(SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS decimal(18,1)) AS K
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate) BETWEEN '2026-09-27' AND '2026-10-02'
GROUP BY CONVERT(date,p.PaymentDate)
ORDER BY D;

PRINT '=== Name hunt sample for receipt names ===';
SELECT TOP 80 c.CustomerID, LEFT(c.CustomerName,45) AS Name
FROM dbo.Customers c
WHERE c.DelegateID=@ListId AND (
  c.CustomerName LIKE N'%عباس عبد الرضا%'
  OR c.CustomerName LIKE N'%حسن رعد%'
  OR c.CustomerName LIKE N'%جواد عبود%'
  OR c.CustomerName LIKE N'%مرتضى تركي%'
  OR c.CustomerName LIKE N'%حسين عباس%'
  OR c.CustomerName LIKE N'%محمد جواد%'
  OR c.CustomerName LIKE N'%هدو%'
  OR c.CustomerName LIKE N'%عواك%'
  OR c.CustomerName LIKE N'%عوادي%'
)
ORDER BY c.CustomerName;
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
sql=Path('/tmp/wilaya2_sep29.sql').read_text(encoding='utf-8-sig')
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
    with sftp.file("/tmp/wilaya2_sep29.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_sep29_runner.py", "w") as f:
        f.write(runner)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_sep29_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_sep29_probe.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
