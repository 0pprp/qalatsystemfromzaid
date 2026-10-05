#!/usr/bin/env python3
"""SQL-only: apply WeakWeek Pending-legal patch on all Admin* DBs."""
from __future__ import annotations

import os
import sys
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
SQL_FILE = ROOT / "DatabaseScripts" / "Deploy" / "LegalConversion_WeakWeek_Pending_Deploy.sql"

REMOTE_PY = r'''
import json, pathlib, subprocess
sql_path = '/tmp/legal_weak_pending.sql'
ok = fail = 0
seen = set()
for apps in sorted(pathlib.Path('/opt/qalaa-runtime').glob('Admin*/appsettings.json')):
    name = apps.parent.name
    if 'backup' in name.lower() or name.endswith('-old'):
        continue
    try:
        cfg = json.loads(apps.read_text(encoding='utf-8', errors='replace'))
    except Exception as ex:
        print('SKIP', name, ex); fail += 1; continue
    cs = (cfg.get('ConnectionStrings') or {}).get('DataBaseConnection') or ''
    parts = {}
    for part in cs.split(';'):
        if '=' in part:
            k, v = part.split('=', 1)
            parts[k.strip().lower()] = v.strip()
    server = parts.get('server') or parts.get('data source')
    db = parts.get('initial catalog') or parts.get('database')
    user = parts.get('user id') or parts.get('uid')
    pwd = parts.get('password') or parts.get('pwd')
    if not all([server, db, user, pwd]):
        print('BAD_CS', name); fail += 1; continue
    key = (server, db)
    if key in seen:
        print('SKIP_DUP', name, db); continue
    seen.add(key)
    r = subprocess.run(
        ['/opt/mssql-tools18/bin/sqlcmd', '-C', '-I', '-S', server, '-d', db, '-U', user, '-P', pwd, '-i', sql_path, '-b'],
        capture_output=True, text=True,
    )
    print('SQL', name, db, 'RC', r.returncode)
    if r.returncode != 0:
        print((r.stdout or '')[-600])
        print((r.stderr or '')[-400])
        fail += 1
        continue
    ok += 1
    q = "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.Customers_GetWeakWeekPayers')) LIKE N'%LegalConversionRequests%' THEN 1 ELSE 0 END;"
    v = subprocess.run(
        ['/opt/mssql-tools18/bin/sqlcmd', '-C', '-I', '-S', server, '-d', db, '-U', user, '-P', pwd, '-h', '-1', '-W', '-Q', q],
        capture_output=True, text=True,
    )
    print('VERIFY', name, (v.stdout or '').strip().splitlines()[:2])
print('DONE ok', ok, 'fail', fail)
raise SystemExit(0 if fail == 0 else 1)
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    sql = SQL_FILE.read_text(encoding="utf-8")
    stamp = time.strftime("%Y%m%d_%H%M%S")

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/legal_weak_pending.sql", "w") as f:
            f.write(sql)
        with sftp.file(f"/tmp/deploy_legal_weak_sql_{stamp}.py", "w") as f:
            f.write(REMOTE_PY)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_legal_weak_sql_{stamp}.py", timeout=900)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
