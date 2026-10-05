#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import re, subprocess, json, base64, urllib.request
from pathlib import Path

app = Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text(encoding='utf-8', errors='replace')
m = re.search(r'"DataBaseConnection"\s*:\s*"([^"]+)"', app)
cs = m.group(1)
parts = {}
for p in cs.split(';'):
    if '=' in p:
        k,v = p.split('=',1)
        parts[k.strip().lower()] = v.strip()
server = parts.get('server') or parts.get('data source')
db = parts.get('database') or parts.get('initial catalog')
user = parts.get('user id') or parts.get('uid')
pwd = parts.get('password') or parts.get('pwd')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'

lines = [
"SET NOCOUNT ON;",
# rating-style: customers with open remaining and last sale date in Sep
"SELECT 'vc_open_sale_sep', COUNT(1) FROM dbo.View_Customers vc WHERE ISNULL(vc.AmountRemaining,0) > 0 AND vc.CustomerID IN (SELECT CustomerID FROM dbo.View_CustomersSalesDelegate WHERE DateCreate >= '2026-09-01' AND DateCreate < '2026-10-01');",
"SELECT 'vc_any_sale_sep', COUNT(1) FROM dbo.View_Customers vc WHERE vc.CustomerID IN (SELECT CustomerID FROM dbo.View_CustomersSalesDelegate WHERE DateCreate >= '2026-09-01' AND DateCreate < '2026-10-01');",
# drafts completed in sep
"IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL SELECT 'drafts_sep', COUNT(DISTINCT CustomerId) FROM dbo.SalesDrafts WHERE Status=N'Completed' AND CustomerId>0 AND CompletedAtUtc >= '2026-08-31' AND CompletedAtUtc < '2026-10-01'; ELSE SELECT 'drafts_sep', -1;",
# customersales date boundaries with time
"SELECT 'cs_to_928', COUNT(DISTINCT CustomerID) FROM dbo.CustomersSales WHERE DateCreate >= '2026-09-01' AND DateCreate < DATEADD(day,1,'2026-09-28');",
"SELECT 'cs_full_sep', COUNT(DISTINCT CustomerID) FROM dbo.CustomersSales WHERE DateCreate >= '2026-09-01' AND DateCreate < '2026-10-01';",
# active delegates customers with sale in period via join like rating lists
"SELECT 'active_list_sales', COUNT(DISTINCT s.CustomerID) FROM dbo.CustomersSales s INNER JOIN dbo.Customers c ON c.CustomerID=s.CustomerID INNER JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID WHERE ISNULL(d.DelegateState,1)=1 AND s.DateCreate >= '2026-09-01' AND s.DateCreate < '2026-10-01';",
]
Path('/tmp/gap.sql').write_text('\n'.join(lines), encoding='utf-8')
cmd = [sqlcmd,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-h','-1','-W','-i','/tmp/gap.sql']
print(subprocess.check_output(cmd, text=True, errors='replace', timeout=90))

# director trend customerCount
payload = json.dumps({'userName':'حيدر','password':'حيدر'}).encode()
req = urllib.request.Request('http://127.0.0.1:5280/api/Auth/LoginDirector', data=payload, headers={'Content-Type':'application/json'})
with urllib.request.urlopen(req, timeout=20) as resp:
    token = json.loads(resp.read().decode())['token']
auth = {'Authorization': f'Bearer {token}'}
url = 'http://127.0.0.1:5280/api/director/stats/trend?cityValue=1&scope=branch&id=0&fromDate=2026-09-01&toDate=2026-09-28'
req = urllib.request.Request(url, headers=auth)
with urllib.request.urlopen(req, timeout=120) as resp:
    t = json.loads(resp.read().decode())
    print('TREND_COUNT', t.get('customerCount') or t.get('CustomerCount'))
    print('EXCELLENT', t.get('excellentCount') or t.get('ExcellentCount'))
    print('sum_buckets', sum(int(t.get(k) or t.get(K) or 0) for k,K in [
        ('excellentCount','ExcellentCount'),('goodCount','GoodCount'),('weakCount','WeakCount'),
        ('rejectedCount','RejectedCount'),('badCount','BadCount'),('legalCount','LegalCount')]))
"""

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file('/tmp/gap75.py', 'w') as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command('python3 /tmp/gap75.py', timeout=180)
        print(stdout.read().decode('utf-8', 'replace'))
        err = stderr.read().decode('utf-8', 'replace')
        if err.strip():
            print('STDERR', err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == '__main__':
    raise SystemExit(main())
