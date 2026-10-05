#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=60,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r'''
python3 - <<'PY'
import json, pathlib, subprocess
apps=pathlib.Path('/opt/qalaa-runtime/AdminBaghdadKarkh/appsettings.json')
cfg=json.loads(apps.read_text(encoding='utf-8', errors='replace'))
cs=(cfg.get('ConnectionStrings') or {}).get('DataBaseConnection') or ''
parts={}
for part in cs.split(';'):
    if '=' in part:
        k,v=part.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
print('DB', server, db, user)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/legal_weak_pending.sql','-b'],capture_output=True,text=True)
print('RC', r.returncode)
print('OUT>>>')
print(r.stdout or '')
print('ERR>>>')
print(r.stderr or '')
# check if View_CustomersSalesDelegate exists
q="SELECT OBJECT_ID(N'dbo.View_CustomersSalesDelegate'), OBJECT_ID(N'dbo.Customers_GetWeakWeekPayers');"
v=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-h','-1','-W','-Q',q],capture_output=True,text=True)
print('OBJ', v.stdout)
PY
'''
_, o, e = c.exec_command(cmd, timeout=120)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:800])
c.close()
