#!/usr/bin/env python3
from __future__ import annotations
import os, sys
from pathlib import Path
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
REMOTE = r'''
import json, os, re, subprocess
from pathlib import Path
def parse_cs(cs):
    def grab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id') or grab('UID'), grab('Password') or grab('Pwd')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'
cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
def run(sql):
    p = subprocess.run([sqlcmd,'-C','-f','65001','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-Q',sql],
                       capture_output=True, text=True, encoding='utf-8', errors='replace')
    print(p.stdout or ''); print(p.stderr or '')
run("SELECT TOP 5 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' ORDER BY ORDINAL_POSITION;")
run("SELECT TOP 5 * FROM dbo.FollowerUserLists WHERE ListId=1;")
run("""
SELECT TOP 20 u.UserId, CONVERT(VARCHAR(100), u.UserName) AS UN,
  CONVERT(VARCHAR(100), u.DelegateName) AS DN
FROM dbo.Users u
WHERE u.UserName LIKE N'%احمد%' OR u.UserName LIKE N'%أحمد%' OR u.DelegateName LIKE N'%احمد%' OR u.DelegateName LIKE N'%أحمد%';
""")
# columns of Users that look like name
run("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND (COLUMN_NAME LIKE '%Name%' OR COLUMN_NAME LIKE '%Follow%' OR COLUMN_NAME LIKE '%Active%');")
print('DONE')
'''
def main():
    if not SSH_PASS: return 2
    c = paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    _, so, se = c.exec_command(f"python3 - <<'PY'\n{REMOTE}\nPY")
    out = so.read().decode('utf-8', errors='replace'); err = se.read().decode('utf-8', errors='replace'); code = so.channel.recv_exit_status()
    Path('_ops_tmp_krar_link_out.txt').write_text(out+err, encoding='utf-8')
    for line in out.splitlines(): print(line.encode('ascii','replace').decode('ascii'))
    c.close(); return code
if __name__ == '__main__': raise SystemExit(main())
