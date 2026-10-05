#!/usr/bin/env python3
"""Apply WeakWeek Pending-legal patch on all Admin* DBs + redeploy FE toast copy."""
from __future__ import annotations

import os
import subprocess
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
SQL_FILE = ROOT / "DatabaseScripts" / "Deploy" / "LegalConversion_WeakWeek_Pending_Deploy.sql"
FE = ROOT / "FE_Company"
FE_DIST = FE / "dist"

REMOTE_PY = r'''
import json, os, pathlib, shutil, subprocess, tarfile
from pathlib import Path
stamp = __STAMP__
sql_path = '/tmp/legal_weak_pending.sql'
fx = Path('/tmp/fe_legal_weak_x_' + stamp)
if fx.exists():
    shutil.rmtree(fx)
fx.mkdir()
with tarfile.open('/tmp/fe_legal_weak_' + stamp + '.tgz') as t:
    t.extractall(fx)
for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
    dest = Path(dest_s)
    if not dest.is_dir():
        continue
    for root, dirs, files in os.walk(fx):
        for f in files:
            src = Path(root) / f
            rel = src.relative_to(fx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('FE', dest_s)
subprocess.run(['systemctl', 'reload', 'nginx'], check=False)

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
        print((r.stdout or '')[-500])
        print((r.stderr or '')[-500])
        fail += 1
    else:
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
    # FE already built in prior failed attempt? rebuild to be safe
    if not (FE_DIST / "index.html").exists():
        print("npm build FE...", flush=True)
        r = subprocess.run(
            ["npm", "run", "build"],
            cwd=str(FE),
            capture_output=True,
            text=True,
            shell=True,
        )
        if r.returncode:
            print(r.stdout[-2500:], r.stderr[-2500:])
            return r.returncode
    else:
        # rebuild so toast copy is current
        print("npm build FE...", flush=True)
        r = subprocess.run(
            ["npm", "run", "build"],
            cwd=str(FE),
            capture_output=True,
            text=True,
            shell=True,
        )
        if r.returncode:
            print(r.stdout[-2500:], r.stderr[-2500:])
            return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    fe_tar = ROOT / f"_fe_legal_weak_{stamp}.tgz"
    if fe_tar.exists():
        fe_tar.unlink()
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))

    remote = REMOTE_PY.replace("__STAMP__", repr(stamp))

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/legal_weak_pending.sql", "w") as f:
            f.write(sql)
        sftp.put(str(fe_tar), f"/tmp/fe_legal_weak_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_legal_weak_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_legal_weak_{stamp}.py", timeout=900)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
