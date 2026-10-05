#!/usr/bin/env python3
import os
from pathlib import Path
import paramiko

REMOTE = r'''
import json, re, subprocess, os
from pathlib import Path

gcfg = json.loads(Path('/opt/qalaa-runtime/DatabaseSalesGateway/appsettings.json').read_text(encoding='utf-8'))
print('keys', list(gcfg.get('ConnectionStrings', {}).keys()))
cs = list(gcfg.get('ConnectionStrings', {}).values())[0]

def grab(key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

server = grab('Data Source') or grab('Server')
db = grab('Initial Catalog') or grab('Database')
user = grab('User ID') or grab('User Id')
pwd = grab('Password')
print('db', db)
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    print(p.stdout or '')
    if p.stderr:
        print(p.stderr)

print('COLS')
run("SET NOCOUNT ON; SELECT TOP 15 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'SalesExceptionRequests' ORDER BY ORDINAL_POSITION;")
print('BY CUST')
run("""
SET NOCOUNT ON;
SELECT TOP 5 CustomerId, Status, CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Notes,N''),80) AS Notes
FROM dbo.SalesExceptionRequests
WHERE CustomerId=53388
ORDER BY CreatedAtUtc DESC;
""")
print('RECENT')
run("""
SET NOCOUNT ON;
SELECT TOP 8 CustomerId, Status, CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Notes,N''),80) AS Notes
FROM dbo.SalesExceptionRequests
ORDER BY CreatedAtUtc DESC;
""")
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    '169.58.236.52',
    username='root',
    password=os.environ['SSH_PASS'],
    timeout=60,
    allow_agent=False,
    look_for_keys=False,
)
sftp = c.open_sftp()
with sftp.file('/tmp/ws53388b.py', 'w') as f:
    f.write(REMOTE)
sftp.close()
_, o, e = c.exec_command('python3 /tmp/ws53388b.py', timeout=60)
out = o.read().decode('utf-8', 'replace')
err = e.read().decode('utf-8', 'replace')
print(out)
if err.strip():
    print('STDERR', err[:1000])
Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_ws_53388.txt').write_text(out, encoding='utf-8')
c.close()
