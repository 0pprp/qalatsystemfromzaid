#!/usr/bin/env python3
"""Find the missing 130K / 2 payments for Wilaya-2 Oct1 to reach receipt 2370/34."""
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
DECLARE @Day date = '2026-10-01';

PRINT '=== Customers of list5 who paid Sep29 and Oct2 same amount but NOT Oct1 ===';
;WITH s29 AS (
  SELECT p.CustomerID, CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2)) AS K
  FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-29'
),
s02 AS (
  SELECT p.CustomerID, CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2)) AS K
  FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
),
s01 AS (
  SELECT DISTINCT CustomerID FROM dbo.CustomersPayments
  WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day
),
s30 AS (
  SELECT DISTINCT p.CustomerID
  FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-30'
)
SELECT s29.CustomerID, s29.K, LEFT(c.CustomerName,40) AS Name
FROM s29
JOIN s02 ON s02.CustomerID=s29.CustomerID AND s02.K=s29.K
LEFT JOIN s01 ON s01.CustomerID=s29.CustomerID
LEFT JOIN s30 ON s30.CustomerID=s29.CustomerID
LEFT JOIN dbo.Customers c ON c.CustomerID=s29.CustomerID
WHERE s01.CustomerID IS NULL AND s30.CustomerID IS NULL
ORDER BY s29.K DESC;

PRINT '=== Any payment Oct1 for list5 customers on OTHER delegate ===';
SELECT p.CustomerPaymentID, p.CustomerID, p.DelegateID, ISNULL(a.Amount,0)*1448/1000.0 AS K,
       LEFT(c.CustomerName,40) AS Name
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE c.DelegateID=@ListId AND p.DelegateID<>@ListId
  AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== Combinations: two payments on Sep30 all-list5 totaling 130 not in Oct1 set ===';
-- show Sep30 payments (all) amounts distribution
SELECT CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2)) AS K, COUNT(*) AS Cnt
FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-09-30'
GROUP BY CAST(ISNULL(a.Amount,0)*1448/1000.0 AS decimal(18,2))
ORDER BY K DESC;

PRINT '=== Box-create Oct1 ALL amounts sum (should be 2275) vs receipt ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K,
       2370.0 - SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS GapToReceipt
FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day;

PRINT '=== If PaymentDate:=BoxCreate for all box-Oct1 ===';
-- simulate
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K
FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day;

PRINT '=== Customers with Oct1 BoxCreate, unique count ===';
SELECT COUNT(DISTINCT p.CustomerID) AS DistinctCust
FROM dbo.CustomersPayments p JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day;
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
sql=Path('/tmp/wilaya2_m130.sql').read_text(encoding='utf-8-sig')
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

def main():
    if not SSH_PASS:
        print('SSH_PASS required'); return 2
    c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp=c.open_sftp()
    with sftp.file('/tmp/wilaya2_m130.sql','w') as f: f.write(SQL)
    with sftp.file('/tmp/wilaya2_m130_runner.py','w') as f: f.write(REMOTE)
    sftp.close()
    _,out,err=c.exec_command('python3 /tmp/wilaya2_m130_runner.py', timeout=180)
    text=out.read().decode('utf-8','replace'); e=err.read().decode('utf-8','replace')
    path=Path(__file__).with_name('_wilaya2_m130_out.txt')
    path.write_text(text+(('\nSSH_ERR\n'+e) if e.strip() else ''), encoding='utf-8')
    print(f'wrote {path} size={path.stat().st_size}'); c.close(); return 0

if __name__=='__main__':
    raise SystemExit(main())
