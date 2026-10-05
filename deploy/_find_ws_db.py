#!/usr/bin/env python3
import os
from pathlib import Path
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('169.58.236.52', username='root', password=os.environ['SSH_PASS'], timeout=60, allow_agent=False, look_for_keys=False)
remote = r'''
import os, json, re, subprocess
from pathlib import Path

def grab(cs, key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

def sqlbin():
    return '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(server, db, user, pwd, sql):
    p = subprocess.run([sqlbin(),'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql],capture_output=True,text=True,encoding='utf-8',errors='replace')
    return ((p.stdout or '')+(p.stderr or '')).strip()

print('LS', os.listdir('/opt/qalaa-runtime/SalesGateway')[:40])
for name in ['appsettings.json','appsettings.Production.json','appsettings.Production.Runtime.json']:
    p = Path('/opt/qalaa-runtime/SalesGateway')/name
    print(name, p.exists(), p.stat().st_size if p.exists() else 0)

# read production env drop-ins for connection
print(subprocess.getoutput('ls -la /etc/systemd/system/qalaa-sales-gateway.service.d/'))
print(subprocess.getoutput('grep -R Connection /etc/systemd/system/qalaa-sales-gateway.service.d/ /etc/qalaa-production/*gateway* /etc/qalaa-production/*sales* 2>/dev/null | head -40'))

# process environ
pid=subprocess.getoutput('systemctl show -p MainPID --value qalaa-sales-gateway').strip()
print('PID', pid)
if pid and pid!='0':
    env=Path(f'/proc/{pid}/environ').read_bytes().split(b'\0')
    for b in env:
        s=b.decode('utf-8','replace')
        if 'Connection' in s or 'Database' in s or 'Sql' in s or 'Exception' in s:
            # mask password
            if 'Password=' in s:
                s=re.sub(r'(Password=)[^;]+', r'\1***', s, flags=re.I)
            print('ENV', s[:200])

cfg_path=Path('/opt/qalaa-runtime/SalesGateway/appsettings.json')
if cfg_path.exists():
    cfg=json.loads(cfg_path.read_text(encoding='utf-8'))
    print('KEYS', list(cfg.keys()))
    print('CS_KEYS', list((cfg.get('ConnectionStrings') or {}).keys()))
    for k,v in (cfg.get('ConnectionStrings') or {}).items():
        if isinstance(v,str):
            print('CS', k, re.sub(r'(Password=)[^;]+', r'\1***', v, flags=re.I)[:160])

# Try DatabaseCompany / gateway DB names via najaf login on master
najaf=json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
ncs=najaf['ConnectionStrings']['DataBaseConnection']
ns,nu,np=grab(ncs,'Data Source') or grab(ncs,'Server'), grab(ncs,'User ID') or grab(ncs,'User Id'), grab(ncs,'Password')
print('DBS')
print(run(ns,'master',nu,np,"SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE '%Sales%' OR name LIKE '%Gateway%' OR name LIKE '%Exception%' OR name LIKE '%Company%' ORDER BY 1;"))

# probe each db for SalesExceptionRequests
dbs=run(ns,'master',nu,np,"SET NOCOUNT ON; SELECT name FROM sys.databases ORDER BY 1;")
for line in dbs.splitlines():
    name=line.strip()
    if not name or name in ('name','----','master','tempdb','model','msdb') or name.startswith('-'):
        continue
    if name.lower() in ('name',):
        continue
    probe=run(ns,name,nu,np,"SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.SalesExceptionRequests',N'U') IS NULL THEN N'NO' ELSE N'YES' END;")
    if 'YES' in probe:
        print('FOUND', name)
        print(run(ns,name,nu,np,"""
SET NOCOUNT ON;
SELECT TOP 20 RequestId, CustomerId, CustomerName, CityValue, Status,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Notes,N''),80) AS Notes
FROM dbo.SalesExceptionRequests
WHERE CustomerId=53388 OR CustomerId=53385
   OR (CreatedAtUtc >= DATEADD(day,-2,SYSUTCDATETIME()) AND Notes LIKE N'%مبيعات خاطئة%')
ORDER BY CreatedAtUtc DESC;
"""))
'''
sftp=c.open_sftp()
with sftp.file('/tmp/find_ws_db.py','w') as f: f.write(remote)
sftp.close()
_,o,e=c.exec_command('python3 /tmp/find_ws_db.py', timeout=180)
print(o.read().decode('utf-8','replace'))
err=e.read().decode('utf-8','replace')
if err.strip(): print('STDERR', err[:2000])
c.close()
