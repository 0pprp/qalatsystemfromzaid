import os, paramiko
from pathlib import Path
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('169.58.236.52', username='root', password=os.environ['SSH_PASS'], timeout=60, allow_agent=False, look_for_keys=False)
REMOTE=r'''
import os, re, subprocess, json
from pathlib import Path

def mask(line):
    if '=' not in line: return line
    k,v=line.split('=',1)
    if any(x in k.lower() for x in ('key','pass','secret','token')):
        return f'{k}=***len{len(v)}***'
    return line

envp='/etc/qalaa-production/delegate-najaf-sales-gateway.env'
print('ENVFILE', envp, 'exists', os.path.isfile(envp))
if os.path.isfile(envp):
    for line in Path(envp).read_text(encoding='utf-8', errors='replace').splitlines():
        if not line.strip() or line.strip().startswith('#'): continue
        print(mask(line))

comp='/etc/qalaa-production/delegate-central-complaints.env'
print('COMPLAINTS', os.path.isfile(comp))
if os.path.isfile(comp):
    for line in Path(comp).read_text(encoding='utf-8', errors='replace').splitlines():
        if not line.strip() or line.strip().startswith('#'): continue
        print(mask(line))

# process env of running najaf delegate
pid=subprocess.getoutput("systemctl show -p MainPID --value qalaa-delegatehaidernajaflast").strip()
print('PID', pid)
if pid and pid!='0':
    try:
        environ=Path(f'/proc/{pid}/environ').read_bytes().split(b'\0')
        for b in environ:
            s=b.decode('utf-8','replace')
            if any(k in s for k in ('SalesGateway','InternalApi','Branch__','Branch:')):
                print(mask(s))
    except Exception as e:
        print('proc env err', e)

# logs WrongSale
print('=== journal WrongSale last day ===')
print(subprocess.getoutput("journalctl -u qalaa-delegatehaidernajaflast --since '2 days ago' --no-pager | grep -iE 'WrongSale|SalesGateway|مبيعات خاطئة|FirstCollection' | tail -40"))

# strings in dll for FirstCollectionEvaluation
dll='/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll'
print('DLL has FirstCollection', b'FirstCollectionEvaluation' in Path(dll).read_bytes())
print('DLL has WrongSaleExceptionForwarder', b'WrongSaleExceptionForwarder' in Path(dll).read_bytes())
print('DLL has ResolveCityValue', b'ResolveCityValue' in Path(dll).read_bytes())

# SalesShopProfiles recent low ratings?
import re, subprocess
cfg=json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
cs=cfg['ConnectionStrings']['DataBaseConnection']
def grab(key):
    m=re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''
server,db,user,pwd=grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id'), grab('Password')
sqlcmd='/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'
def run(sql):
    p=subprocess.run([sqlcmd,'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql],capture_output=True,text=True,encoding='utf-8',errors='replace')
    return p.stdout or ''
print('=== SalesShopProfiles cols ===')
print(run("SET NOCOUNT ON; SELECT TOP 1 * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SalesShopProfiles'; SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SalesShopProfiles' ORDER BY ORDINAL_POSITION;"))
print('=== recent shop profiles ===')
print(run("""
SET NOCOUNT ON;
SELECT TOP 20 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'SalesShopProfiles';
"""))
'''
_,o,e=c.exec_command("python3 - <<'PY'\n"+REMOTE+"\nPY")
out=o.read().decode('utf-8','replace')
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_diag3.txt').write_text(out, encoding='utf-8')
print(out.encode('ascii','replace').decode('ascii'))
c.close()
