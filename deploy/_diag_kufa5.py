import os, paramiko
from pathlib import Path
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('169.58.236.52', username='root', password=os.environ['SSH_PASS'], timeout=60, allow_agent=False, look_for_keys=False)
REMOTE=r'''
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
    return (p.stdout or '') + (('\n'+p.stderr) if p.stderr else '')

print('=== journal FirstCollection / rejected / 400 / 403 ===')
print(subprocess.getoutput("journalctl -u qalaa-delegatehaidernajaflast --since '2 days ago' --no-pager | grep -iE 'FirstCollection|forward rejected|المحافظة|WrongSale exception|SalesRequests' | tail -60"))

# najaf company DB for sales requests with wrong sale notes
cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text(encoding='utf-8')) if os.path.isfile('/opt/qalaa-runtime/AdminNajaf/appsettings.json') else json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
# prefer AdminNajaf if exists
for path in ['/opt/qalaa-runtime/AdminNajaf/appsettings.json','/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json']:
    if os.path.isfile(path):
        cfg=json.loads(Path(path).read_text(encoding='utf-8')); print('CFG', path); break
server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('DB', db)

print('=== SalesRequests with WrongSale notes last 3d ===')
print(run(server,db,user,pwd,"""
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL BEGIN SELECT N'NO_SR' AS Msg; END
ELSE
SELECT TOP 30 Id, Status, CustomerName, CustomerPhone, CreatedByName, CreatedByUserType, ExistingCustomerId, CreatedAtUtc,
  LEFT(ISNULL(Notes,N''), 120) AS NotesHead
FROM dbo.SalesRequests
WHERE CreatedAtUtc >= DATEADD(day,-3,SYSUTCDATETIME())
  AND (Notes LIKE N'%مبيعات خاطئة%' OR Notes LIKE N'%تقييم المتجر%' OR Notes LIKE N'%المصدر: مندوب%')
ORDER BY CreatedAtUtc DESC;
"""))

print('=== FollowerSaleAuthentications rating<=6 last 3d ===')
print(run(server,db,user,pwd,"""
SET NOCOUNT ON;
SELECT TOP 30 a.Id, a.CustomerId, a.ListId, a.Rating, a.FollowerUserId, a.AuthenticatedAtUtc, c.CustomerName, d.DelegateName AS ListName
FROM dbo.FollowerSaleAuthentications a
LEFT JOIN dbo.Customers c ON c.CustomerID=a.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID=a.ListId
WHERE a.Rating <= 6 AND a.AuthenticatedAtUtc >= DATEADD(day,-3,SYSUTCDATETIME())
ORDER BY a.AuthenticatedAtUtc DESC;
"""))

# gateway exceptions DB
print('=== gateway WrongSale exceptions ===')
for path in ['/opt/qalaa-runtime/SalesGateway/appsettings.json','/opt/qalaa-demo/sales-gateway/appsettings.json']:
    if not os.path.isfile(path): continue
    g=json.loads(Path(path).read_text(encoding='utf-8'))
    cdict=g.get('ConnectionStrings') or {}
    print('GW path', path, 'keys', list(cdict.keys()))
    for key,val in cdict.items():
        s,d,u,pw=parse_cs(val)
        if not d: continue
        print(' try', key, d)
        out=run(s,d,u,pw,"""
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.SalesExceptionRequests', N'U') IS NULL BEGIN SELECT N'NO' AS Msg; END
ELSE
SELECT TOP 40 Id, CustomerId, CustomerName, CityValue, CityName, Status, ReadAtUtc, RequestingManagerDisplayName, RequestedAtUtc,
  LEFT(Reason,100) AS ReasonHead
FROM dbo.SalesExceptionRequests
WHERE RequestedAtUtc >= DATEADD(day,-3,SYSUTCDATETIME())
  AND (Reason LIKE N'%مبيعات خاطئة%' OR Reason LIKE N'%تقييم المتجر%')
ORDER BY RequestedAtUtc DESC;
""")
        print(out[:5000])
'''
_,o,e=c.exec_command("python3 - <<'PY'\n"+REMOTE+"\nPY")
out=o.read().decode('utf-8','replace')
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_diag4.txt').write_text(out, encoding='utf-8')
print(out.encode('ascii','replace').decode('ascii'))
c.close()
