#!/usr/bin/env python3
"""CreatedDate vs PaymentDate; AdminNajaf collection posting logs; force PostEligible."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;
DECLARE @ListId INT = 5;
DECLARE @Today date = CONVERT(date, DATEADD(HOUR,3,SYSUTCDATETIME()));

PRINT '=== Today list5 PaymentDate vs CreatedDate ===';
SELECT p.CustomerPaymentID,
       CONVERT(varchar(19), p.PaymentDate, 120) AS PaymentDate,
       CONVERT(varchar(19), p.CreatedDate, 120) AS CreatedDate,
       p.ClientPaymentId,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Today
ORDER BY p.CustomerPaymentID;

PRINT '=== Payments created today (CreatedDate) for list5 regardless PaymentDate ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.CreatedDate)=@Today;

PRINT '=== Payments with CreatedDate today but PaymentDate <> today list5 ===';
SELECT TOP 20 p.CustomerPaymentID,
       CONVERT(varchar(19), p.PaymentDate, 120) AS PaymentDate,
       CONVERT(varchar(19), p.CreatedDate, 120) AS CreatedDate,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.CreatedDate)=@Today
  AND CONVERT(date,p.PaymentDate)<>@Today
ORDER BY p.CustomerPaymentID DESC;

PRINT '=== Run PostEligible now ===';
DECLARE @Posted INT;
EXEC dbo.CustomersPaymentsRequest_PostEligible @UserCreateID=1, @PostedCount=@Posted OUTPUT;
SELECT @Posted AS PostedNow;
SELECT COUNT(*) AS RequestsLeft FROM dbo.CustomersPaymentsRequest;

PRINT '=== AmountDenar today via view ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(AmountDenar,0)) AS bigint) AS SumDenar
FROM dbo.View_CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Today;
"""

REMOTE_LOGS = r"""
echo '=== AdminNajaf journal last 200 ==='
journalctl -u qalaa-company-sharenewnajaf.service --no-pager -n 200 2>/dev/null | tail -200
echo '=== DelegateNajaf journal last 100 ==='
journalctl -u qalaa-delegatehaidernajaflast.service --no-pager -n 100 2>/dev/null | tail -100
echo '=== grep collection/post in najaf journals ==='
journalctl -u qalaa-company-sharenewnajaf.service --no-pager --since '2026-09-23' 2>/dev/null | grep -iE 'Collection|PostEligible|auto-posted|payment' | tail -50
echo '=== appsettings CollectionPayments ==='
python3 - <<'PY'
import json
from pathlib import Path
cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
print('CollectionPayments', cfg.get('CollectionPayments'))
print('ConnDB', (cfg.get('ConnectionStrings') or {}).get('DataBaseConnection','')[:80])
PY
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)

_, o1, e1 = c.exec_command(REMOTE_LOGS, timeout=60)
print(o1.read().decode("utf-8", "replace"))
if e1.read():
    pass

sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_created.sql", "w") as f:
    f.write(SQL)
runner = r"""
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
sql=Path('/tmp/wilaya2_created.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin; break
r=subprocess.run(cmd, capture_output=True, text=True)
print(r.stdout or '')
print('STDERR', (r.stderr or '')[-2000:])
os.unlink(path)
"""
with sftp.file("/tmp/wilaya2_created_runner.py", "w") as f:
    f.write(runner)
sftp.close()
_, o2, e2 = c.exec_command("python3 /tmp/wilaya2_created_runner.py", timeout=120)
print(o2.read().decode("utf-8", "replace"))
print(e2.read().decode("utf-8", "replace")[-1000:])
c.close()
