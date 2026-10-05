#!/usr/bin/env python3
"""Schema + Wilaya-2 request/payment gap probe on Najaf."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;

PRINT '=== CustomersPaymentsRequest columns ===';
SELECT name, TYPE_NAME(user_type_id) AS typ FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.CustomersPaymentsRequest') ORDER BY column_id;

PRINT '=== CustomersPayments columns ===';
SELECT name, TYPE_NAME(user_type_id) AS typ FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.CustomersPayments') ORDER BY column_id;

PRINT '=== Delegates ولاية/نجف ===';
SELECT DelegateID, DelegateName FROM dbo.Delegates
WHERE DelegateName LIKE N'%ولاية%' OR DelegateName LIKE N'%نجف%'
ORDER BY DelegateID;

PRINT '=== SP PostEligible exists? ===';
SELECT OBJECT_ID('dbo.CustomersPaymentsRequest_PostEligible','P') AS PostEligibleOid;

PRINT '=== HostedService / recent request sample (top 3 raw) ===';
SELECT TOP 3 * FROM dbo.CustomersPaymentsRequest ORDER BY CustomersPaymentsRequestID DESC;
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
print('DB', db, 'SERVER', server)
sql=Path('/tmp/wilaya2_schema.sql').read_text(encoding='utf-8-sig')
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
  print('STDERR', r.stderr[-2000:])
print('exit', r.returncode)
os.unlink(path)
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_schema.sql", "w") as f:
    f.write(SQL)
with sftp.file("/tmp/wilaya2_schema_runner.py", "w") as f:
    f.write(RUNNER)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/wilaya2_schema_runner.py", timeout=120)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR", e[-2000:])
c.close()
