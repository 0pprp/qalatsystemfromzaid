from pathlib import Path
import os, paramiko
HOST="169.58.236.52"
SSH_PASS=os.environ["SSH_PASS"]
REMOTE=r'''
import json, os, re, subprocess
from pathlib import Path

def parse_cs(cs):
    def grab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id') or grab('UID'), grab('Password') or grab('Pwd')

sqlcmd='/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run_sql(server, db, user, pwd, sql):
    p=subprocess.run([sqlcmd,'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql],capture_output=True,text=True,encoding='utf-8',errors='replace')
    return (p.stdout or '')+(('\n'+p.stderr) if p.stderr else '')

# 1) All delegate appsettings with SalesGateway / InternalApiKey
print('=== Delegate appsettings SalesGateway scan ===')
for root in ['/opt/qalaa-runtime','/opt/qalaa-demo']:
    if not os.path.isdir(root): continue
    for name in sorted(os.listdir(root)):
        if 'delegate' not in name.lower() and 'Delegate' not in name: continue
        p=os.path.join(root,name,'appsettings.json')
        if not os.path.isfile(p): continue
        try:
            d=json.loads(Path(p).read_text(encoding='utf-8'))
        except Exception as e:
            print('BAD', p, e); continue
        gw=d.get('SalesGateway') or {}
        key=bool(d.get('InternalApiKey') or gw.get('ApiKey'))
        base=gw.get('BaseUrl')
        branch=(d.get('Branch') or {})
        if base or key or branch.get('CityValue') or 'Najaf' in name or 'najaf' in name.lower():
            print(name, 'BaseUrl=', base, 'Key=', key, 'BranchCity=', branch.get('CityValue'), 'BranchName=', branch.get('CityName'))

# 2) Najaf Last full relevant keys
p='/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json'
d=json.loads(Path(p).read_text(encoding='utf-8'))
print('=== NajafLast keys ===')
for k in sorted(d.keys()):
    if k.lower() in ('connectionstrings','logging'): continue
    v=d[k]
    if isinstance(v, dict):
        print(k, list(v.keys()), {kk: ('***' if 'key' in kk.lower() or 'pass' in kk.lower() else vv) for kk,vv in v.items()})
    else:
        print(k, '***' if 'key' in k.lower() or 'pass' in k.lower() else v)

# 3) Find FirstCollection table names
cfg=d
server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('=== Tables like %Eval% / %First% / %Wrong% ===')
print(run_sql(server,db,user,pwd,"""
SET NOCOUNT ON;
SELECT name FROM sys.tables WHERE name LIKE N'%Eval%' OR name LIKE N'%First%' OR name LIKE N'%Wrong%' OR name LIKE N'%Shop%' ORDER BY name;
"""))

# 4) Kufa delegates
print('=== Kufa delegates ===')
print(run_sql(server,db,user,pwd,"""
SET NOCOUNT ON;
SELECT TOP 30 DelegateID, DelegateName, CityID FROM dbo.Delegates WHERE DelegateName LIKE N'%كوف%' OR DelegateName LIKE N'%كوفة%';
"""))

# 5) Gateway config sample from working unit
for path in ['/opt/qalaa-runtime/SalesGateway/appsettings.json','/opt/qalaa-demo/sales-gateway/appsettings.json','/opt/qalaa-runtime/DelegateHaiderBasra/appsettings.json','/opt/qalaa-runtime/DelegateHaiderKarbala/appsettings.json']:
    if os.path.isfile(path):
        g=json.loads(Path(path).read_text(encoding='utf-8'))
        print('SAMPLE', path)
        print(' SalesGateway', g.get('SalesGateway'))
        print(' Branch', g.get('Branch'))
        print(' InternalApiKey', 'SET' if g.get('InternalApiKey') else 'MISSING')
'''
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
_,o,e=c.exec_command("python3 - <<'PY'\n"+REMOTE+"\nPY")
out=o.read().decode('utf-8','replace'); err=e.read().decode('utf-8','replace')
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_diag2.txt').write_text(out+('\n'+err if err.strip() else ''), encoding='utf-8')
print(out.encode('ascii','replace').decode('ascii'))
c.close()
