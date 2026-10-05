#!/usr/bin/env python3
"""Gap3: complete SQL after fixing batch abort + count nginx payment posts Oct1."""
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
DECLARE @Day date = '2026-10-01';
DECLARE @ListId INT = 5;

PRINT '=== 1) Mis-attributed payments (cust list5, pay other list) ===';
SELECT COUNT(*) AS Cnt, ISNULL(SUM(ISNULL(a.Amount,0)*1448)/1000.0,0) AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE c.DelegateID=@ListId AND p.DelegateID<>@ListId
  AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== 2) Box create date vs payment date ===';
SELECT
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)=@Day THEN 1 ELSE 0 END) AS ByPayDateCnt,
  ISNULL(SUM(CASE WHEN CONVERT(date,p.PaymentDate)=@Day THEN ISNULL(a.Amount,0)*1448 ELSE 0 END)/1000.0,0) AS ByPayDateK,
  SUM(CASE WHEN CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day THEN 1 ELSE 0 END) AS ByBoxCreateCnt,
  ISNULL(SUM(CASE WHEN CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day THEN ISNULL(a.Amount,0)*1448 ELSE 0 END)/1000.0,0) AS ByBoxCreateK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND (CONVERT(date,p.PaymentDate)=@Day OR CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day);

PRINT '=== 3) Request rows near Oct1 CreatedAtUtc ===';
SELECT COUNT(*) AS ReqNear, ISNULL(SUM(ISNULL(Amount,0))/1000.0,0) AS AmtK
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND CreatedAtUtc >= '2026-09-30T21:00:00'
  AND CreatedAtUtc < '2026-10-02T21:00:00';

PRINT '=== 4) Last 15 request rows list5 ever ===';
SELECT TOP 15 CustomersPaymentsRequestID, CustomerID, Amount/1000.0 AS AmtK,
       CONVERT(varchar(19), PaymentDate, 120) AS PaymentDate,
       CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
       CASE WHEN PostedToBoxAtUtc IS NULL THEN 0 ELSE 1 END AS Posted,
       LEFT(CONVERT(varchar(64), ClientPaymentId), 36) AS ClientPaymentId
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId
ORDER BY CustomersPaymentsRequestID DESC;

PRINT '=== 5) Neighbor days ===';
SELECT CONVERT(varchar(10), CONVERT(date,p.PaymentDate), 120) AS D,
       COUNT(*) AS Cnt,
       SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate) BETWEEN '2026-09-28' AND '2026-10-03'
GROUP BY CONVERT(date,p.PaymentDate)
ORDER BY D;

PRINT '=== 6) Full Oct1 payment running sum ===';
SELECT p.CustomerPaymentID, p.CustomerID,
       CONVERT(varchar(19), p.PaymentDate, 120) AS PaymentDate,
       ISNULL(a.Amount,0)*1448/1000.0 AS IqdK,
       SUM(ISNULL(a.Amount,0)*1448/1000.0) OVER (ORDER BY p.PaymentDate, p.CustomerPaymentID) AS RunK,
       LEFT(CONVERT(varchar(64), p.ClientPaymentId), 36) AS ClientPaymentId
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY p.PaymentDate, p.CustomerPaymentID;

PRINT '=== 7) AmountDaySales sum for list5 customers now ===';
IF COL_LENGTH('dbo.View_Customers','AmountDaySales') IS NOT NULL
  SELECT COUNT(*) AS N,
         SUM(ISNULL(AmountDaySales,0))/1000.0 AS DaySalesK
  FROM dbo.View_Customers WHERE DelegateID=@ListId AND ISNULL(AmountDaySales,0)<>0;

PRINT '=== 8) Null ClientPaymentId Oct1 ===';
SELECT COUNT(*) AS NullIdCnt,
       ISNULL(SUM(ISNULL(a.Amount,0)*1448)/1000.0,0) AS IqdK
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day AND p.ClientPaymentId IS NULL;

PRINT '=== 9) Payments Oct1 for list5 WITHOUT AddToBox ===';
SELECT COUNT(*) AS NoBox
FROM dbo.CustomersPayments p
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
  AND NOT EXISTS (SELECT 1 FROM dbo.AddToBox a WHERE a.CustomerPaymentID=p.CustomerPaymentID);

PRINT '=== 10) Global Oct1 ClientPaymentId dupes (any list) ===';
SELECT TOP 20 ClientPaymentId, COUNT(*) AS Cnt
FROM dbo.CustomersPayments
WHERE CONVERT(date,PaymentDate)=@Day AND ClientPaymentId IS NOT NULL
GROUP BY ClientPaymentId
HAVING COUNT(*)>1
ORDER BY Cnt DESC;
"""

REMOTE = r"""
import json, os, subprocess, tempfile, gzip, re
from pathlib import Path
from datetime import datetime

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
sql=Path('/tmp/wilaya2_oct1_gap3.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','120','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-2000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)

print('=== NGINX payment posts Oct1 ===')
# scan access logs for payment request endpoints on Oct 01
patterns = ('CustomersPayments','Payment','payments')
hits = {'total':0,'2xx':0,'4xx':0,'5xx':0,'timeoutish':0}
samples=[]
log_dirs=['/var/log/nginx']
files=[]
for d in log_dirs:
  if not os.path.isdir(d):
    continue
  for name in os.listdir(d):
    p=os.path.join(d,name)
    if not os.path.isfile(p):
      continue
    if 'access' not in name:
      continue
    files.append(p)
files.sort()
print('log_files', len(files))

def open_log(p):
  if p.endswith('.gz'):
    return gzip.open(p,'rt',errors='replace')
  return open(p,'rt',errors='replace')

for p in files:
  try:
    with open_log(p) as f:
      for line in f:
        if '01/Oct/2026' not in line and '2026-10-01' not in line:
          continue
        low=line.lower()
        if 'payment' not in low and 'customerspayments' not in low:
          continue
        hits['total']+=1
        m=re.search(r'\s(\d{3})\s', line)
        code=int(m.group(1)) if m else 0
        if 200<=code<300: hits['2xx']+=1
        elif 400<=code<500: hits['4xx']+=1
        elif 500<=code<600: hits['5xx']+=1
        if code in (499,504,502,408): hits['timeoutish']+=1
        if len(samples)<30 and ('delegate' in low or 'najaf' in low or 'payment' in low):
          samples.append(line.strip()[:240])
  except Exception as ex:
    print('log_err', p, ex)

print('payment_http', hits)
for s in samples[:20]:
  print('SAMPLE', s)

print('=== journal PostPaymentIdempotent count Oct1 ===')
j=subprocess.run(['bash','-lc',"journalctl -u 'qalaa-delegate*' --since '2026-10-01 00:00' --until '2026-10-02 00:00' --no-pager 2>/dev/null | grep -c 'PostPaymentIdempotent' || true"], capture_output=True, text=True)
print('PostPaymentIdempotent_mentions', (j.stdout or '').strip())
j2=subprocess.run(['bash','-lc',"journalctl -u 'qalaa-delegate*' --since '2026-10-01 00:00' --until '2026-10-02 00:00' --no-pager 2>/dev/null | grep -c 'Error Number:-2' || true"], capture_output=True, text=True)
print('timeout_minus2', (j2.stdout or '').strip())
j3=subprocess.run(['bash','-lc',"journalctl -u 'qalaa-delegate*' --since '2026-10-01 00:00' --until '2026-10-02 00:00' --no-pager 2>/dev/null | grep -c 'Error Number:2714' || true"], capture_output=True, text=True)
print('constraint_2714', (j3.stdout or '').strip())
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required"); return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_oct1_gap3.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_oct1_gap3_runner.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_oct1_gap3_runner.py", timeout=300)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    path = Path(__file__).with_name("_wilaya2_oct1_gap3_out.txt")
    path.write_text(text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8")
    print(f"wrote {path} size={path.stat().st_size}")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
