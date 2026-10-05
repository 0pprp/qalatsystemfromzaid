#!/usr/bin/env python3
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

PRINT '=== View columns ===';
SELECT c.name FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.View_CustomersPayments') ORDER BY c.column_id;

PRINT '=== View def first 2500 ===';
SELECT SUBSTRING(OBJECT_DEFINITION(OBJECT_ID('dbo.View_CustomersPayments')),1,2500);

PRINT '=== Flags Oct1 ===';
SELECT p.CustomerPaymentID, p.CustomerID, p.SelectState, p.DelegateState, p.AccountZero, p.AsyncState, p.BoxID,
       ISNULL(a.Amount,0)*1448/1000.0 AS IqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY p.CustomerPaymentID;

PRINT '=== Sep30-boxed customers NOT on Oct1 PayDate ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-09-30'
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments p2
    WHERE p2.DelegateID=@ListId AND p2.CustomerID=p.CustomerID AND CONVERT(date,p2.PaymentDate)=@Day
  );

SELECT p.CustomerPaymentID, p.CustomerID, ISNULL(a.Amount,0)*1448/1000.0 AS IqdK,
       LEFT(ISNULL(c.CustomerName,''),40) AS Name
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-09-30'
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments p2
    WHERE p2.DelegateID=@ListId AND p2.CustomerID=p.CustomerID AND CONVERT(date,p2.PaymentDate)=@Day
  )
ORDER BY IqdK DESC;

PRINT '=== If move ALL non-overlap Sep30-boxed to Oct1 ===';
SELECT
  21 + COUNT(*) AS NewCnt,
  1855.0 + SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS NewK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-09-30'
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments p2
    WHERE p2.DelegateID=@ListId AND p2.CustomerID=p.CustomerID AND CONVERT(date,p2.PaymentDate)=@Day
  );
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
sql=Path('/tmp/wilaya2_view2.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','300','-s','|','-i',path], capture_output=True, text=True)
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
    with sftp.file('/tmp/wilaya2_view2.sql','w') as f: f.write(SQL)
    with sftp.file('/tmp/wilaya2_view2_runner.py','w') as f: f.write(REMOTE)
    sftp.close()
    _,out,err=c.exec_command('python3 /tmp/wilaya2_view2_runner.py', timeout=180)
    text=out.read().decode('utf-8','replace'); e=err.read().decode('utf-8','replace')
    path=Path(__file__).with_name('_wilaya2_view2_out.txt')
    path.write_text(text+(('\nSSH_ERR\n'+e) if e.strip() else ''), encoding='utf-8')
    print(f'wrote {path} size={path.stat().st_size}'); c.close(); return 0

if __name__=='__main__':
    raise SystemExit(main())
