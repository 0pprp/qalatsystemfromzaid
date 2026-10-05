#!/usr/bin/env python3
"""Why CustomerSaleId on draft has no CustomersSales row."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;
PRINT '=== Draft 66 detail ===';
SELECT SaleId, CustomerId, CustomerSaleId, FullName, Phone, PostingStatus, PostedAtUtc
FROM dbo.SalesDrafts WHERE SaleId=66;

PRINT '=== Customer 53383 ===';
SELECT CustomerID, CustomerName, PhoneNumber, CustomerState FROM dbo.Customers WHERE CustomerID=53383;

PRINT '=== Any sales near 55211 ===';
SELECT TOP 10 CustomerSaleID, CustomerID, DateCreate FROM dbo.CustomersSales
WHERE CustomerSaleID BETWEEN 55200 AND 55220 OR CustomerID=53383
ORDER BY CustomerSaleID DESC;

PRINT '=== Max CustomerSaleID ===';
SELECT MAX(CustomerSaleID) AS MaxId FROM dbo.CustomersSales;

PRINT '=== SalesRequest 194 ===';
SELECT Id, CustomerName, CustomerPhone, ConvertedToSaleId, Status, ExceptionHoldStatus, ActiveExceptionId
FROM dbo.SalesRequests WHERE Id=194;
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
sql=Path('/tmp/cascade_why.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin; break
r=subprocess.run(cmd, capture_output=True, text=True)
print(r.stdout or '')
print(r.stderr or '')
os.unlink(path)
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/cascade_why.sql", "w") as f:
    f.write(SQL)
with sftp.file("/tmp/cascade_why_runner.py", "w") as f:
    f.write(RUNNER)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/cascade_why_runner.py", timeout=60)
print(out.read().decode("utf-8", "replace"))
c.close()
