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

# find sqlcmd
cands = list(Path('/opt').rglob('sqlcmd')) + list(Path('/usr').rglob('sqlcmd'))
print('sqlcmd cands', cands[:5])
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

# try pymssql / pyodbc
ok=False
for mod in ('pymssql','pyodbc'):
    try:
        __import__(mod)
        print('HAS', mod)
        ok=True
    except Exception:
        print('NO', mod)

if 'pymssql' in globals() or True:
    try:
        import pymssql
        conn = pymssql.connect(server=server, user=user, password=pwd, database=db, login_timeout=15)
        cur = conn.cursor()
        q = [
            ("customersales_all", "SELECT COUNT(DISTINCT CustomerID) FROM dbo.CustomersSales WHERE DateCreate >= '2026-09-01' AND DateCreate < '2026-10-01'"),
            ("customersales_first", "SELECT COUNT(DISTINCT c.CustomerID) FROM dbo.Customers c WHERE EXISTS (SELECT 1 FROM dbo.CustomersSales s WHERE s.CustomerID=c.CustomerID AND s.DateCreate>='2026-09-01' AND s.DateCreate<'2026-10-01') AND NOT EXISTS (SELECT 1 FROM dbo.CustomersSales s2 WHERE s2.CustomerID=c.CustomerID AND s2.DateCreate<'2026-09-01')"),
            ("view_all", "SELECT COUNT(DISTINCT CustomerID) FROM dbo.View_CustomersSalesDelegate WHERE DateCreate >= '2026-09-01' AND DateCreate < '2026-10-01'"),
            ("sales_rows", "SELECT COUNT(1) FROM dbo.CustomersSales WHERE DateCreate >= '2026-09-01' AND DateCreate < '2026-10-01'"),
        ]
        for name, sql in q:
            cur.execute(sql)
            print(name, cur.fetchone()[0])
        conn.close()
    except Exception as ex:
        print('pymssql err', type(ex).__name__, ex)
"""

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/sql_month_cmp2.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/sql_month_cmp2.py", timeout=90)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
