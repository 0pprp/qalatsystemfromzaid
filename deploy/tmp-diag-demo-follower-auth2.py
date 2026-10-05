#!/usr/bin/env python3
from __future__ import annotations
import os
import paramiko

cmd = r"""
python3 - <<'PY'
import json,subprocess
cfg=json.load(open('/opt/qalaa-demo/follower-backend/appsettings.json',encoding='utf-8'))
cs=cfg['ConnectionStrings']['DataBaseConnection']
parts={}
for part in cs.split(';'):
    if '=' in part:
        k,v=part.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
sql='''SET NOCOUNT ON;
SELECT DB_NAME();
SELECT TOP 5 COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' ORDER BY ORDINAL_POSITION;
SELECT UserID, UserName, UserType, UserState, AsyncID, AsyncState
FROM dbo.Users
WHERE UserName=N'ahmed4' OR AsyncID=N'QKj3hJFcPGe' OR UserID=10;
SELECT TOP 20 UserID, UserName, UserType, UserState, AsyncID
FROM dbo.Users WHERE UserType LIKE N'%متابع%' OR UserType LIKE N'%تابع%';
IF OBJECT_ID('dbo.FollowerUserLists','U') IS NOT NULL
  SELECT TOP 20 * FROM dbo.FollowerUserLists;
ELSE
  SELECT 'NO_FollowerUserLists' AS Info;
'''
open('/tmp/fdiag2.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/fdiag2.sql','-W','-s','|','-f','65001'],capture_output=True,text=True)
print(r.stdout)
print(r.stderr[:400] if r.stderr else '')
print('RC', r.returncode)
PY
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace"))
print(stderr.read().decode("utf-8", "replace")[:500])
c.close()
