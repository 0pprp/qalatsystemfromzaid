#!/usr/bin/env python3
import os, json, re, subprocess
from pathlib import Path
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('169.58.236.52', username='root', password=os.environ['SSH_PASS'], timeout=60, allow_agent=False, look_for_keys=False)
remote = r'''
import json, re, subprocess, os
from pathlib import Path
gcfg = json.loads(Path('/opt/qalaa-runtime/DatabaseSalesGateway/appsettings.json').read_text(encoding='utf-8'))
cs = gcfg['ConnectionStrings'].get('DataBaseConnection') or gcfg['ConnectionStrings'].get('DefaultConnection')
def grab(key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''
server, db, user, pwd = grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id'), grab('Password')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'
sql = """
SET NOCOUNT ON;
SELECT TOP 10 RequestId, CustomerId, CustomerName, CityValue, Status,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Notes,N''),60) AS Notes
FROM dbo.SalesExceptionRequests
WHERE CustomerId = 53388 OR Notes LIKE N'%53388%'
ORDER BY CreatedAtUtc DESC;
"""
p = subprocess.run([sqlcmd,'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql], capture_output=True, text=True, encoding='utf-8', errors='replace')
print(p.stdout or '')
print(p.stderr or '')
'''
sftp = c.open_sftp()
with sftp.file('/tmp/check_ws_53388.py','w') as f: f.write(remote)
sftp.close()
_,o,e=c.exec_command('python3 /tmp/check_ws_53388.py', timeout=60)
print(o.read().decode('utf-8','replace'))
c.close()
