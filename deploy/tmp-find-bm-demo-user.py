import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=30,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
python3 <<'PY'
import json,subprocess,os
# find demo company connection
paths=[
 '/opt/qalaa-runtime/AdminNajaf/appsettings.json',
 '/opt/qalaa-demo/AdminNajaf/appsettings.json',
]
# list demo services
import glob
print('demo units:')
os.system("systemctl list-units --type=service --all --no-legend | grep -i demo | head -20")
# try env for demo company
for p in glob.glob('/etc/qalaa-production/*demo*')+glob.glob('/etc/qalaa-demo/**/*.env', recursive=True)[:20]:
  print('ENVFILE', p)
# query BM users from najaf demo db if we can find cs
for cfgp in glob.glob('/opt/qalaa-runtime/**/appsettings.json', recursive=False):
  pass
# common demo path
candidates=glob.glob('/opt/qalaa-runtime/*Demo*/appsettings.json')+glob.glob('/opt/qalaa-runtime/*DEMO*/appsettings.json')+glob.glob('/opt/qalaa-demo/**/appsettings.json', recursive=True)
print('CFG', candidates[:10])
for cfgp in candidates[:5]+['/opt/qalaa-runtime/AdminNajaf/appsettings.json']:
  if not os.path.exists(cfgp):
    continue
  cfg=json.load(open(cfgp,encoding='utf-8'))
  cs=cfg.get('ConnectionStrings',{}).get('DataBaseConnection')
  if not cs: continue
  print('USING', cfgp)
  parts={}
  for part in cs.split(';'):
    if '=' in part:
      k,v=part.split('=',1); parts[k.strip().lower()]=v.strip()
  server=parts.get('server') or parts.get('data source')
  db=parts.get('initial catalog') or parts.get('database')
  user=parts.get('user id') or parts.get('uid')
  pwd=parts.get('password') or parts.get('pwd')
  print('DB', db)
  sql='''SET NOCOUNT ON;
SELECT TOP 10 UserID, UserName, UserType FROM dbo.Users WHERE UserType=N'مدير فرع' AND ISNULL(UserState,1)=1;
'''
  open('/tmp/bm_users.sql','w').write(sql)
  r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/bm_users.sql','-W','-s','|'],capture_output=True,text=True)
  print(r.stdout)
  print('RC', r.returncode)
  break
PY
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace")[:5000])
print(stderr.read().decode("utf-8", "replace")[:1000])
c.close()
