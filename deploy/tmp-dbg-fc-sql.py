#!/usr/bin/env python3
import os
import traceback

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, subprocess, traceback
from pathlib import Path

cfg = Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json")
data = json.loads(cfg.read_text(encoding="utf-8-sig"))
cs = (data.get("ConnectionStrings") or {}).get("DataBaseConnection") or ""
parts = {}
for p in cs.split(";"):
    if "=" in p:
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get("server") or parts.get("data source")
db = parts.get("database") or parts.get("initial catalog")
user = parts.get("user id") or parts.get("uid")
pwd = parts.get("password") or parts.get("pwd")
print("ARGS", server, db, user, "pwdlen", len(pwd or ""))
print("sqlcmd exists", Path("/opt/mssql-tools18/bin/sqlcmd").is_file())
print("sql exists", Path("/tmp/fc_col.sql").is_file(), "size", Path("/tmp/fc_col.sql").stat().st_size if Path("/tmp/fc_col.sql").is_file() else 0)
try:
    r = subprocess.run(
        ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-I", "-S", server, "-d", db, "-U", user, "-P", pwd,
         "-i", "/tmp/fc_col.sql", "-b"],
        capture_output=True, text=True,
    )
    print("RC", r.returncode)
    print("OUT", (r.stdout or "")[-800:])
    print("ERR", (r.stderr or "")[-400:])
except Exception:
    traceback.print_exc()
'''


def main() -> int:
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/dbg_fc_sql.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/dbg_fc_sql.py", timeout=120)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err)
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
