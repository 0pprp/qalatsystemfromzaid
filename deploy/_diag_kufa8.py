import os, re, json, subprocess
from pathlib import Path
import paramiko
HOST='169.58.236.52'
SSH_PASS=os.environ['SSH_PASS']
remote=r"""
import json, os, re, subprocess
from pathlib import Path
sqlcmd='/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def parse_cs(cs):
    def grab(key):
        m=re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id') or grab('UID'), grab('Password') or grab('Pwd')

def run(server,db,user,pwd,sql):
    p=subprocess.run([sqlcmd,'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql],capture_output=True,text=True,encoding='utf-8',errors='replace')
    return (p.stdout or '')+(('\n'+p.stderr) if p.stderr else '')

# get CS for DatabaseSalesGateway from delegated-manager.env or SalesGateway folder
print('delegated-manager.env:')
for line in Path('/etc/qalaa-production/delegated-manager.env').read_text(encoding='utf-8',errors='replace').splitlines():
    if not line.strip() or line.startswith('#'): continue
    k=line.split('=',1)[0]
    if any(x in k.lower() for x in ('pass','key','secret')): print(k+'=***')
    else: print(line)

# try extract connection from SalesGateway dll folder - maybe appsettings missing due to deploy skip
print('SalesGateway files', os.listdir('/opt/qalaa-runtime/SalesGateway')[:40] if os.path.isdir('/opt/qalaa-runtime/SalesGateway') else 'NO DIR')

# Use najaf CS server credentials against DatabaseSalesGateway
cfg=json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
s,db,u,pw=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('=== SalesExceptionRequests in DatabaseSalesGateway ===')
print(run(s,'DatabaseSalesGateway',u,pw,"""
SET NOCOUNT ON;
SELECT COUNT(1) AS Total FROM dbo.SalesExceptionRequests;
SELECT TOP 40 Id, CustomerId, CustomerName, CityValue, CityName, Status, ReadAtUtc, RequestingManagerDisplayName, RequestedAtUtc,
  LEFT(Reason,120) AS ReasonHead
FROM dbo.SalesExceptionRequests
ORDER BY RequestedAtUtc DESC;
"""))

print('=== WrongSale only last 3d ===')
print(run(s,'DatabaseSalesGateway',u,pw,"""
SET NOCOUNT ON;
SELECT TOP 40 Id, CustomerId, CustomerName, CityValue, CityName, Status, ReadAtUtc, RequestingManagerDisplayName, RequestedAtUtc,
  LEFT(Reason,120) AS ReasonHead
FROM dbo.SalesExceptionRequests
WHERE RequestedAtUtc >= DATEADD(day,-3,SYSUTCDATETIME())
  AND (Reason LIKE N'%مبيعات خاطئة%' OR Reason LIKE N'%تقييم المتجر%')
ORDER BY RequestedAtUtc DESC;
"""))
"""
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username='root',password=SSH_PASS,timeout=60,allow_agent=False,look_for_keys=False)
_,o,e=c.exec_command("python3 - <<'PY'\n"+remote+"\nPY")
out=o.read().decode('utf-8','replace')
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_diag7.txt').write_text(out,encoding='utf-8')
print(out.encode('ascii','replace').decode('ascii')[:12000])
c.close()
