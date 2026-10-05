#!/usr/bin/env python3
"""Detect same-customer same-amount Sep30/Oct1 pairs for Wilaya-2; quantify double risk."""
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

PRINT '=== Same customer+amount on BOTH 2026-09-30 and 2026-10-01 ===';
;WITH a AS (
  SELECT p.CustomerID, CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2)) AS IqdK,
         COUNT(*) AS Cnt30, MIN(p.CustomerPaymentID) AS Pay30
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-30'
  GROUP BY p.CustomerID, CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2))
),
b AS (
  SELECT p.CustomerID, CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2)) AS IqdK,
         COUNT(*) AS Cnt01, MIN(p.CustomerPaymentID) AS Pay01
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-01'
  GROUP BY p.CustomerID, CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2))
)
SELECT a.CustomerID, a.IqdK, a.Cnt30, b.Cnt01, a.Pay30, b.Pay01
FROM a JOIN b ON a.CustomerID=b.CustomerID AND a.IqdK=b.IqdK
ORDER BY a.IqdK DESC;

SELECT COUNT(*) AS PairCnt, SUM(a.IqdK) AS SuspectedDoubleK
FROM (
  SELECT a.CustomerID, a.IqdK
  FROM (
    SELECT p.CustomerID, CAST(ISNULL(bx.Amount,0)*1448/1000.0 AS decimal(18,2)) AS IqdK
    FROM dbo.CustomersPayments p
    JOIN dbo.AddToBox bx ON bx.CustomerPaymentID=p.CustomerPaymentID
    WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-30'
    GROUP BY p.CustomerID, CAST(ISNULL(bx.Amount,0)*1448/1000.0 AS decimal(18,2))
  ) a
  JOIN (
    SELECT p.CustomerID, CAST(ISNULL(bx.Amount,0)*1448/1000.0 AS decimal(18,2)) AS IqdK
    FROM dbo.CustomersPayments p
    JOIN dbo.AddToBox bx ON bx.CustomerPaymentID=p.CustomerPaymentID
    WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-01'
    GROUP BY p.CustomerID, CAST(ISNULL(bx.Amount,0)*1448/1000.0 AS decimal(18,2))
  ) b ON a.CustomerID=b.CustomerID AND a.IqdK=b.IqdK
) a;

PRINT '=== Oct1 PaymentDate total + Sep30-payments boxed on Oct1 ===';
SELECT
  (SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
   FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
   WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-01') AS PayDate01K,
  (SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
   FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
   WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-30'
     AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))='2026-10-01') AS Sep30BoxedOct1K,
  (SELECT COUNT(*)
   FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
   WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-01') AS Cnt01,
  (SELECT COUNT(*)
   FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
   WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-30'
     AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))='2026-10-01') AS CntSep30Boxed;

PRINT '=== What admin GetAll might show: SP definition peek ===';
SELECT TOP 1 OBJECT_DEFINITION(OBJECT_ID('dbo.GetCustomersPaymentsByDateDelegate')) AS Def;

PRINT '=== Big 1500 payment detail ===';
SELECT p.CustomerPaymentID, p.CustomerID, c.CustomerName,
       CONVERT(varchar(19),p.PaymentDate,120) AS PayDate,
       CONVERT(varchar(19),ISNULL(a.CreatedDate,a.DateCreate),120) AS BoxCreate,
       ISNULL(a.Amount,0)*1448/1000.0 AS IqdK,
       p.ClientPaymentId
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.CustomerPaymentID=418682;
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
sql=Path('/tmp/wilaya2_oct1_dup.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i',path], capture_output=True, text=True)
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
    with sftp.file("/tmp/wilaya2_oct1_dup.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_oct1_dup_runner.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_oct1_dup_runner.py", timeout=180)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    path = Path(__file__).with_name("_wilaya2_oct1_dup_out.txt")
    path.write_text(text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8")
    print(f"wrote {path} size={path.stat().st_size}")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
