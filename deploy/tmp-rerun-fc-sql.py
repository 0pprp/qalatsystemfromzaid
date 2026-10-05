#!/usr/bin/env python3
"""Re-run FollowerCollectionEnabled SQL on all Admin* DBs."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
SQL = ROOT / "DatabaseScripts" / "Migrations" / "20261002_Delegates_FollowerCollectionEnabled.sql"
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    sql_text = SQL.read_text(encoding="utf-8")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/fc_col.sql", "w") as f:
            f.write(sql_text)
        sftp.close()

        remote = r'''
import json, subprocess
from pathlib import Path
admins = __ADMINS__
SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"
sql_path = "/tmp/fc_col.sql"

def parse_cs(cs: str):
    parts = {}
    for p in (cs or "").split(";"):
        p = p.strip()
        if not p or "=" not in p:
            continue
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source") or "",
        parts.get("database") or parts.get("initial catalog") or "",
        parts.get("user id") or parts.get("uid") or parts.get("user") or "",
        parts.get("password") or parts.get("pwd") or "",
    )

ok = 0
for name in admins:
    cfg = Path("/opt/qalaa-runtime") / name / "appsettings.json"
    if not cfg.is_file():
        print("SKIP", name); continue
    try:
        raw = cfg.read_text(encoding="utf-8-sig")
        data = json.loads(raw)
        cs = ((data.get("ConnectionStrings") or {}).get("DataBaseConnection")
              or (data.get("ConnectionStrings") or {}).get("DefaultConnection")
              or "")
        server, db, user, pwd = parse_cs(cs)
        print("CS", name, "server=", bool(server), "db=", db, "user=", bool(user), "pwd=", bool(pwd))
        if not (server and db and user and pwd):
            # dump keys only
            print("BAD", name, list((data.get("ConnectionStrings") or {}).keys()))
            continue
        r = subprocess.run(
            [SQLCMD, "-C", "-I", "-S", server, "-d", db, "-U", user, "-P", pwd, "-i", sql_path, "-b"],
            capture_output=True, text=True,
        )
        if r.returncode:
            print("FAIL", name, (r.stdout or "")[-500], (r.stderr or "")[-500])
        else:
            ok += 1
            print("OK", name, db)
    except Exception as e:
        print("ERR", name, type(e).__name__, e)
print("DONE", ok, "/", len(admins))
'''
        remote = remote.replace("__ADMINS__", repr(ADMINS))
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/run_fc_sql.py", "w") as f:
                f.write(remote)
        _, o, e = c.exec_command("python3 /tmp/run_fc_sql.py", timeout=300)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
