import os, json, re, subprocess
from pathlib import Path
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

remote = r"""
import json, os, re, subprocess
from pathlib import Path
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def parse_cs(cs):
    def grab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return (
        grab('Data Source') or grab('Server'),
        grab('Initial Catalog') or grab('Database'),
        grab('User ID') or grab('User Id') or grab('UID'),
        grab('Password') or grab('Pwd'),
    )

def run(server, db, user, pwd, sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    return (p.stdout or '') + (('\n' + p.stderr) if p.stderr else '')

for path in ['/opt/qalaa-runtime/SalesGateway/appsettings.json', '/opt/qalaa-demo/sales-gateway/appsettings.json']:
    print('FILE', path, os.path.isfile(path))
    if not os.path.isfile(path):
        continue
    d = json.loads(Path(path).read_text(encoding='utf-8'))
    print(' CS keys', list((d.get('ConnectionStrings') or {}).keys()))
    for k, v in (d.get('ConnectionStrings') or {}).items():
        s, db, u, pw = parse_cs(v)
        print(' ', k, '=>', db)

print(subprocess.getoutput('systemctl cat qalaa-sales-gateway | sed -n "1,120p"'))
print('DROPINS')
print(subprocess.getoutput('ls -la /etc/systemd/system/qalaa-sales-gateway.service.d/ 2>/dev/null'))
for f in Path('/etc/qalaa-production').glob('*'):
    name = f.name.lower()
    if any(x in name for x in ('gateway', 'sales', 'employee', 'central', 'exception')):
        print('---', f)
        for line in f.read_text(encoding='utf-8', errors='replace').splitlines():
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            if '=' in line:
                k, v = line.split('=', 1)
                if any(x in k.lower() for x in ('key', 'pass', 'secret')):
                    print(k + '=***')
                else:
                    print(line)

cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
s, db, u, pw = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('=== DBs ===')
print(run(s, 'master', u, pw, "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE state_desc='ONLINE' ORDER BY name;"))

# probe known central-ish names
cands = [
    'DatabaseCompanyCentral', 'SalesEmployee', 'QalaaCentral', 'CentralInbox',
    'DatabaseSalesEmployee', 'BE_SalesEmployee', 'DatabaseCompanyGateway',
    'DatabaseCompanyNajaf', 'DatabaseCompanyNajaf_DEMO',
]
for name in cands:
    out = run(s, 'master', u, pw, f"SET NOCOUNT ON; IF DB_ID(N'{name}') IS NULL SELECT 'NO' ELSE SELECT 'YES';")
    if 'YES' in out:
        has = run(s, name, u, pw, "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.SalesExceptionRequests',N'U') IS NULL THEN 0 ELSE COUNT(1) END AS C FROM dbo.SalesExceptionRequests;")
        print('CAND', name, has.splitlines()[:6])
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
_, o, e = c.exec_command("python3 - <<'PY'\n" + remote + "\nPY")
out = o.read().decode('utf-8', 'replace')
err = e.read().decode('utf-8', 'replace')
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_diag6.txt').write_text(out + ('\n' + err if err.strip() else ''), encoding='utf-8')
print(out.encode('ascii', 'replace').decode('ascii')[:12000])
c.close()
