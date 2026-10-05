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
echo '=== delegate najaf env ==='
cat /etc/qalaa-production/delegate-najaf-sales-gateway.env 2>/dev/null | head -40
echo '=== sample ShopImageKey ==='
python3 <<'PY'
import subprocess, os
text=open('/opt/qalaa-runtime/AdminNajaf/appsettings.json',encoding='utf-8',errors='replace').read()
# get connection from env files
cs=None
for path in ['/etc/qalaa-production/delegate-najaf-sales-gateway.env','/etc/qalaa-production/sales-gateway-branch-key.env']:
    if not os.path.exists(path): continue
    for line in open(path,encoding='utf-8',errors='replace'):
        if 'ConnectionString' in line or 'ConnectionStrings' in line:
            print('LINE', path, line[:120])
# query via sqlcmd using AdminNajaf appsettings
import json
cfg=json.load(open('/opt/qalaa-runtime/AdminNajaf/appsettings.json',encoding='utf-8'))
cs=cfg.get('ConnectionStrings',{}).get('DataBaseConnection') or cfg.get('ConnectionStrings',{}).get('DefaultConnection')
print('CS_KEYS', list(cfg.get('ConnectionStrings',{}).keys()))
if not cs:
    # try env in service drop-in
    print(open('/etc/systemd/system/qalaa-company-sharenewnajaf.service',encoding='utf-8',errors='replace').read())
    raise SystemExit(0)
parts={}
for p in cs.split(';'):
    if '=' in p:
        k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
sql='''SET NOCOUNT ON;
SELECT TOP 5 SaleId, ShopImageKey FROM dbo.SalesShopProfiles WHERE ShopImageKey IS NOT NULL;
SELECT TOP 5 Id, FileKey FROM dbo.SalesCustomerDocuments;'''
open('/tmp/media_keys.sql','w').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/media_keys.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout)
print('RC',r.returncode,r.stderr[:300] if r.stderr else '')
PY
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace")[:7000])
print(stderr.read().decode("utf-8", "replace")[:1500])
c.close()
