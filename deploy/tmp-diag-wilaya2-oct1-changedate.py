#!/usr/bin/env python3
"""Explain 2275 box-create vs 1855 payment-date; inspect ChangeDate impact for Wilaya-2 Oct1."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
DECLARE @Day date = '2026-10-01';
DECLARE @ListId INT = 5;

PRINT '=== A) Box created Oct1 but PaymentDate != Oct1 ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND CONVERT(date, p.PaymentDate)<>@Day;

SELECT p.CustomerPaymentID, p.CustomerID,
       CONVERT(varchar(19), p.PaymentDate, 120) AS PaymentDate,
       CONVERT(varchar(19), ISNULL(a.CreatedDate,a.DateCreate), 120) AS BoxCreate,
       ISNULL(a.Amount,0)*1448/1000.0 AS IqdK,
       LEFT(CONVERT(varchar(64), p.ClientPaymentId), 36) AS ClientPaymentId
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND CONVERT(date, p.PaymentDate)<>@Day
ORDER BY p.PaymentDate, p.CustomerPaymentID;

PRINT '=== B) PaymentDate Oct1 but Box create != Oct1 ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, p.PaymentDate)=@Day
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))<>@Day;

PRINT '=== C) All box-create Oct1 (35) with PaymentDate ===';
SELECT CONVERT(varchar(10), CONVERT(date,p.PaymentDate),120) AS PayD,
       COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))=@Day
GROUP BY CONVERT(date,p.PaymentDate)
ORDER BY PayD;

PRINT '=== D) Total if we use BoxCreate as business day (delegate وارد?) ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))=@Day;

PRINT '=== E) View_ReceiptCustomerDate — what date column? ===';
SELECT c.name FROM sys.columns c
WHERE c.object_id=OBJECT_ID('dbo.View_ReceiptCustomerDate')
ORDER BY c.column_id;

PRINT '=== F) Receipt view by PaymentDate vs alternatives Oct1 ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(AmountDenar,0))/1000.0 AS K
FROM dbo.View_ReceiptCustomerDate v
JOIN dbo.Customers c ON c.CustomerID=v.CustomerID
WHERE c.DelegateID=@ListId AND CONVERT(date,v.PaymentDate)=@Day;

PRINT '=== G) Request table count all + list5 ===';
SELECT
  (SELECT COUNT(*) FROM dbo.CustomersPaymentsRequest) AS AllReq,
  (SELECT COUNT(*) FROM dbo.CustomersPaymentsRequest WHERE DelegateID=@ListId) AS List5Req;

PRINT '=== H) ChangeDate procedure / related ===';
SELECT name FROM sys.procedures WHERE name LIKE '%ChangeDate%' OR name LIKE '%Payment%Date%';
SELECT name FROM sys.objects WHERE name LIKE '%ChangeDate%';

PRINT '=== I) Payments with PaymentDate moved to Sep30/Oct2 that had box Oct1 ===';
SELECT CONVERT(varchar(10), CONVERT(date,p.PaymentDate),120) AS PayD,
       COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS IqdK
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND CONVERT(date,p.PaymentDate) IN ('2026-09-30','2026-10-02','2026-10-03','2026-09-29')
GROUP BY CONVERT(date,p.PaymentDate);

PRINT '=== J) Gap to 2370 from box-create 2275 ===';
SELECT 2370.0 - (SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date, ISNULL(a.CreatedDate,a.DateCreate))=@Day) AS MissingFrom2370;
"""

REMOTE = r"""
import json, os, subprocess, tempfile, re, gzip
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
sql=Path('/tmp/wilaya2_oct1_cd.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','140','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-2000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)

print('=== ChangeDate calls mentioning 2026-10-01 ===')
n=0
for root,dirs,files in os.walk('/var/log/nginx'):
  for name in files:
    if 'access' not in name: continue
    p=os.path.join(root,name)
    opener=gzip.open if name.endswith('.gz') else open
    try:
      with opener(p,'rt',errors='replace') as f:
        for line in f:
          if 'ChangeDate' not in line: continue
          if '2026-10-01' not in line and '2026-09-30' not in line: continue
          n+=1
          if n<=40:
            print(line.strip()[:260])
    except Exception as ex:
      print('err', p, ex)
print('ChangeDate_lines', n)

# Delegate PostPaymentIdempotent HTTP status from nginx if path known
print('=== PostPaymentIdempotent nginx Oct1 ===')
cnt={'total':0,'2xx':0,'4xx':0,'5xx':0,'499':0}
for root,dirs,files in os.walk('/var/log/nginx'):
  for name in files:
    if 'access' not in name: continue
    p=os.path.join(root,name)
    opener=gzip.open if name.endswith('.gz') else open
    try:
      with opener(p,'rt',errors='replace') as f:
        for line in f:
          if 'PostPaymentIdempotent' not in line and 'PostSelectPayment' not in line:
            continue
          if '01/Oct/2026' not in line and '2026-10-01' not in line:
            # nginx time format 01/Oct/2026
            if '/Oct/2026' not in line and 'Oct/2026' not in line:
              continue
          if '01/Oct/2026' not in line:
            continue
          cnt['total']+=1
          m=re.search(r'"\s(\d{3})\s', line)
          code=int(m.group(1)) if m else 0
          if 200<=code<300: cnt['2xx']+=1
          elif 400<=code<500: cnt['4xx']+=1
          elif code>=500: cnt['5xx']+=1
          if code==499: cnt['499']+=1
    except Exception:
      pass
print('idempotent_http', cnt)
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required"); return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_oct1_cd.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_oct1_cd_runner.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_oct1_cd_runner.py", timeout=300)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    path = Path(__file__).with_name("_wilaya2_oct1_cd_out.txt")
    path.write_text(text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8")
    print(f"wrote {path} size={path.stat().st_size}")
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
