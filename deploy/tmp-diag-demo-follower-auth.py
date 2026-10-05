#!/usr/bin/env python3
"""Diagnose why demo follower Lists returns 401 for valid login asyncId."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

cmd = r"""
set -e
echo '=== service unit ==='
systemctl cat qalaa-demo-follower | sed -n '1,80p'
echo
echo '=== appsettings (redact password value lengths) ==='
python3 - <<'PY'
import json,re
p='/opt/qalaa-demo/follower-backend/appsettings.json'
cfg=json.load(open(p,encoding='utf-8'))
cs=cfg.get('ConnectionStrings',{})
for k,v in cs.items():
    s=str(v)
    s=re.sub(r'(Password|Pwd)=([^;]+)','\\1=***',s,flags=re.I)
    print(k, '=', s)
print('PublicApiBaseUrl', cfg.get('PublicApiBaseUrl'))
print('keys', list(cfg.keys()))
PY
echo
echo '=== SQL check ahmed4 / asyncId ==='
python3 - <<'PY'
import json,re,subprocess,tempfile,os
cfg=json.load(open('/opt/qalaa-demo/follower-backend/appsettings.json',encoding='utf-8'))
cs=cfg['ConnectionStrings'].get('DataBaseConnection') or list(cfg['ConnectionStrings'].values())[0]
parts={}
for part in cs.split(';'):
    if '=' in part:
        k,v=part.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
print('DB', server, db, user)
sql='''SET NOCOUNT ON;
SELECT DB_NAME() AS DbName;
SELECT TOP 5 UserId, UserName, UserType, AsyncId, IsActive FROM dbo.Users WHERE UserName LIKE N'%ahmed%' OR AsyncId=N'QKj3hJFcPGe';
SELECT TOP 5 DelegateId, DelegateName, AsyncId, FatherId, CityID, UserType FROM dbo.Delegates WHERE DelegateName LIKE N'%ahmed%' OR AsyncId=N'QKj3hJFcPGe' OR DelegateId=10;
SELECT TOP 20 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Delegates' ORDER BY ORDINAL_POSITION;
SELECT TOP 20 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' ORDER BY ORDINAL_POSITION;
'''
open('/tmp/fdiag.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/fdiag.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout)
print('RC', r.returncode)
print(r.stderr[:500] if r.stderr else '')
PY
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace"))
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[:1000])
c.close()
