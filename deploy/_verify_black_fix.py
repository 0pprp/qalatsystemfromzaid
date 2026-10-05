#!/usr/bin/env python3
import os
from pathlib import Path
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=60,
    allow_agent=False,
    look_for_keys=False,
)
remote = r'''
from pathlib import Path
import json, re, subprocess, os
dll = Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll').read_bytes()
print('ForwardWrongSaleInBackground', b'ForwardWrongSaleInBackground' in dll)
print('ResolveCityValue', b'ResolveCityValue' in dll)
print('active', subprocess.getoutput('systemctl is-active qalaa-delegatehaidernajaflast'))
cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
cs = cfg['ConnectionStrings']['DataBaseConnection']
def grab(key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''
server, db, user, pwd = grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id'), grab('Password')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'
def run(sql):
    p = subprocess.run([sqlcmd,'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql], capture_output=True, text=True, encoding='utf-8', errors='replace')
    return (p.stdout or '') + (p.stderr or '')
print('=== recent DFCE ===')
print(run("""
SET NOCOUNT ON;
SELECT TOP 10 Id, CustomerId, DelegateId, Rating,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Note,N''),40) AS Note
FROM dbo.DelegateFirstCollectionEvaluations
ORDER BY CreatedAtUtc DESC;
"""))
print('=== journal background since restart ===')
print(subprocess.getoutput("journalctl -u qalaa-delegatehaidernajaflast --since '3 minutes ago' --no-pager | grep -iE 'Background WrongSale|WrongSale|FirstCollection' | tail -20 || true"))
'''
path = '/tmp/verify_black_fix.py'
sftp = c.open_sftp()
with sftp.file(path, 'w') as f:
    f.write(remote)
sftp.close()
_, o, e = c.exec_command(f'python3 {path}', timeout=120)
print(o.read().decode('utf-8', 'replace'))
err = e.read().decode('utf-8', 'replace')
if err.strip():
    print('STDERR', err[:1000])
c.close()
