#!/usr/bin/env python3
"""Deeper Wilaya-2 payment analysis: amounts, ClientPaymentId, today vs week."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;
DECLARE @ListId INT = 5;
DECLARE @Today date = CONVERT(date, DATEADD(HOUR,3,SYSUTCDATETIME()));
DECLARE @WeekStart date = DATEADD(day, -6, @Today);
DECLARE @MonthStart date = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);

PRINT '=== View_CustomersPayments columns ===';
SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.View_CustomersPayments') ORDER BY column_id;

PRINT '=== Payments + amounts via AddToBox for list 5 ===';
SELECT
  COUNT(*) AS PayCnt,
  SUM(ISNULL(a.Amount,0)*1448) AS AmountIqd,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)=@Today THEN 1 ELSE 0 END) AS TodayCnt,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)=@Today THEN ISNULL(a.Amount,0)*1448 ELSE 0 END) AS TodayIqd,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)>=@WeekStart THEN 1 ELSE 0 END) AS WeekCnt,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)>=@WeekStart THEN ISNULL(a.Amount,0)*1448 ELSE 0 END) AS WeekIqd,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)>=@MonthStart THEN 1 ELSE 0 END) AS MonthCnt,
  SUM(CASE WHEN CONVERT(date,p.PaymentDate)>=@MonthStart THEN ISNULL(a.Amount,0)*1448 ELSE 0 END) AS MonthIqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId;

PRINT '=== Exact 247 / 767 hunt: day counts near those totals ===';
-- cumulative from today backward until ~247 and ~767
;WITH d AS (
  SELECT CONVERT(date, PaymentDate) AS D, COUNT(*) AS Cnt
  FROM dbo.CustomersPayments WHERE DelegateID=@ListId
  GROUP BY CONVERT(date, PaymentDate)
)
SELECT TOP 40 D, Cnt,
  SUM(Cnt) OVER (ORDER BY D DESC ROWS UNBOUNDED PRECEDING) AS CumFromToday
FROM d
ORDER BY D DESC;

PRINT '=== ClientPaymentId coverage recent 14d ===';
SELECT
  SUM(CASE WHEN p.ClientPaymentId IS NULL THEN 1 ELSE 0 END) AS NullClientId,
  SUM(CASE WHEN p.ClientPaymentId IS NOT NULL THEN 1 ELSE 0 END) AS HasClientId,
  COUNT(*) AS Total
FROM dbo.CustomersPayments p
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate) >= @WeekStart;

PRINT '=== All lists today payment counts ===';
SELECT TOP 20 d.DelegateID, d.DelegateName, COUNT(*) AS TodayCnt
FROM dbo.CustomersPayments p
JOIN dbo.Delegates d ON d.DelegateID=p.DelegateID
WHERE CONVERT(date,p.PaymentDate)=@Today
GROUP BY d.DelegateID, d.DelegateName
ORDER BY TodayCnt DESC;

PRINT '=== Mis-attributed? payments where customer.DelegateID != payment.DelegateID for list 5 customers ===';
SELECT COUNT(*) AS Mismatch
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE c.DelegateID=@ListId AND p.DelegateID<>@ListId
  AND CONVERT(date,p.PaymentDate)>=@MonthStart;

SELECT COUNT(*) AS PayOnList5ButCustomerElsewhere
FROM dbo.CustomersPayments p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND ISNULL(c.DelegateID,-1)<>@ListId
  AND CONVERT(date,p.PaymentDate)>=@MonthStart;

PRINT '=== Requests table truly empty? ===';
SELECT COUNT(*) AS ReqAll FROM dbo.CustomersPaymentsRequest;
SELECT COUNT(*) AS ReqAnyHistory FROM dbo.CustomersPaymentsRequest; -- same

PRINT '=== Journal / soft-delete of requests? ===';
SELECT name FROM sys.tables WHERE name LIKE '%Payment%Request%' OR name LIKE '%Payment%Hist%' OR name LIKE '%Archive%';

PRINT '=== Sample today payments list 5 with amounts ===';
SELECT TOP 20 p.CustomerPaymentID, p.CustomerID, p.PaymentDate, p.ClientPaymentId,
       ISNULL(a.Amount,0)*1448 AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Today
ORDER BY p.CustomerPaymentID DESC;
"""

RUNNER = r"""
import json, subprocess, tempfile, os
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
print('DB', db, 'Now check')
sql=Path('/tmp/wilaya2_deep.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql')
os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin
    break
r=subprocess.run(cmd, capture_output=True, text=True)
print(r.stdout or '')
if r.stderr:
  print('STDERR', r.stderr[-3000:])
print('exit', r.returncode)
os.unlink(path)
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_deep.sql", "w") as f:
    f.write(SQL)
with sftp.file("/tmp/wilaya2_deep_runner.py", "w") as f:
    f.write(RUNNER)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/wilaya2_deep_runner.py", timeout=180)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR", e[-2000:])
c.close()
