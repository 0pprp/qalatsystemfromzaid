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
    return (p.stdout or '')+(('\n'+p.stderr) if p.stderr else '')

# find SalesExceptionRequests across DBs / configs
print('=== find SalesExceptionRequests ===')
# from gateway env / appsettings
for path in subprocess.getoutput("find /opt/qalaa-runtime /opt/qalaa-demo /etc/qalaa-production -name 'appsettings.json' 2>/dev/null | head -80").splitlines():
    try:
        d=json.loads(Path(path).read_text(encoding='utf-8'))
    except: continue
    for k,v in (d.get('ConnectionStrings') or {}).items():
        if not isinstance(v,str) or 'Database' not in v and 'Initial Catalog' not in v: continue
        s,db,u,pw=parse_cs(v)
        if not db: continue
        out=run(s,db,u,pw,"SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.SalesExceptionRequests',N'U') IS NULL THEN 0 ELSE 1 END AS HasExc;")
        if '|1' in out.replace(' ','') or out.strip().endswith('1') or '\n1\n' in out or out.splitlines()[-1:].count('1'):
            if '1' in out and 'HasExc' in out:
                # check if has rows
                cnt=run(s,db,u,pw,"SET NOCOUNT ON; SELECT COUNT(1) AS C FROM dbo.SalesExceptionRequests;")
                if 'C' in cnt:
                    print('FOUND', path, k, db, cnt.splitlines()[:5])

print('=== http access FirstCollection ===')
print(subprocess.getoutput("journalctl -u qalaa-delegatehaidernajaflast --since '2 days ago' --no-pager | grep -i FirstCollection | tail -20"))
print(subprocess.getoutput("grep -h FirstCollection /var/log/nginx/*najaf* 2>/dev/null | tail -20 || true"))
print(subprocess.getoutput("grep -rh FirstCollection /var/log/nginx/ 2>/dev/null | tail -20 || true"))

# Kufa delegate id 7 - recent payments / activity
cfg=json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
s,db,u,pw=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('=== recent payments listId/delegate 7 ===')
print(run(s,db,u,pw,"""
SET NOCOUNT ON;
SELECT TOP 20 CustomerPaymentID, CustomerID, Amount, PaymentDate, BoundNumber, DelegateID
FROM dbo.CustomersPayments
WHERE DelegateID=7 AND PaymentDate >= DATEADD(day,-3,GETDATE())
ORDER BY PaymentDate DESC;
"""))
print('=== WrongSaleReports if any ===')
print(run(s,db,u,pw,"""
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.WrongSaleReports',N'U') IS NULL SELECT N'NO_WSR' AS Msg;
ELSE SELECT TOP 20 * FROM dbo.WrongSaleReports ORDER BY 1 DESC;
"""))
'''
_,o,e=c.exec_command("python3 - <<'PY'\n"+REMOTE+"\nPY")
out=o.read().decode('utf-8','replace')
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_diag5.txt').write_text(out, encoding='utf-8')
print(out.encode('ascii','replace').decode('ascii')[:8000])
c.close()
