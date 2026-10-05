#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import re, subprocess
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
"DECLARE @MonthStart datetime = '2026-09-01';",
"DECLARE @MonthEnd datetime = '2026-10-01';",
"SELECT 'customersales_all', COUNT(DISTINCT CustomerID) FROM dbo.CustomersSales WHERE DateCreate >= @MonthStart AND DateCreate < @MonthEnd;",
"SELECT 'customersales_first', COUNT(DISTINCT c.CustomerID) FROM dbo.Customers c WHERE EXISTS (SELECT 1 FROM dbo.CustomersSales s WHERE s.CustomerID=c.CustomerID AND s.DateCreate>=@MonthStart AND s.DateCreate<@MonthEnd) AND NOT EXISTS (SELECT 1 FROM dbo.CustomersSales s2 WHERE s2.CustomerID=c.CustomerID AND s2.DateCreate<@MonthStart);",
"SELECT 'view_all', COUNT(DISTINCT CustomerID) FROM dbo.View_CustomersSalesDelegate WHERE DateCreate >= @MonthStart AND DateCreate < @MonthEnd;",
"SELECT 'sales_rows', COUNT(1) FROM dbo.CustomersSales WHERE DateCreate >= @MonthStart AND DateCreate < @MonthEnd;",
]
Path('/tmp/month_cmp.sql').write_text('\n'.join(lines), encoding='utf-8')
cmd = [sqlcmd,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-h','-1','-W','-i','/tmp/month_cmp.sql']
out = subprocess.check_output(cmd, text=True, errors='replace', timeout=60)
print(out)
"""

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/sql_month_cmp3.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/sql_month_cmp3.py", timeout=90)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
