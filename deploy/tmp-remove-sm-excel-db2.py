#!/usr/bin/env python3
"""Restore SM display names + soft-disable Excel users in province DBs via sqlcmd."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''#!/usr/bin/env python3
import json, os, subprocess
from pathlib import Path

REMOVE = ["fat", "zai", "ban", "jin", "amar1"]
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
KEEP = [
    ("karrar", "karrar123", "مدير المبيعات كرار"),
    ("saad", "saad123", "مدير المبيعات سعد"),
    ("ayman", "ayman123", "مدير المبيعات ايمن"),
]
UNITS = ["qalaa-sales-gateway", "qalaa-demo-sales-gateway"]
SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"

def parse_cs(cs):
    parts = {}
    for p in cs.split(";"):
        p = p.strip()
        if not p or "=" not in p:
            continue
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("database") or parts.get("initial catalog"),
        parts.get("user id") or parts.get("uid") or parts.get("user"),
        parts.get("password") or parts.get("pwd"),
    )

def write_dropin(unit, accounts):
    drop_dir = f"/etc/systemd/system/{unit}.service.d"
    os.makedirs(drop_dir, exist_ok=True)
    path = f"{drop_dir}/sales-manager-accounts.conf"
    lines = ["[Service]"]
    for i in range(12):
        for key in ("UserName", "Password", "DisplayName"):
            full = f"SalesManagerAccounts__{i}__{key}"
            if i < len(accounts):
                u, p, d = accounts[i]
                val = {"UserName": u, "Password": p, "DisplayName": d}[key]
                lines.append(f'Environment={full}="{val}"')
            else:
                lines.append(f'Environment={full}=""')
    Path(path).write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("WROTE", path, "kept", len(accounts))

print("=== RESTORE SM KEEP LIST ===")
for unit in UNITS:
    if subprocess.call(["systemctl", "cat", unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
        print("SKIP_UNIT", unit)
        continue
    write_dropin(unit, KEEP)
subprocess.check_call(["systemctl", "daemon-reload"])
for unit in UNITS:
    if subprocess.call(["systemctl", "cat", unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
        continue
    subprocess.check_call(["systemctl", "restart", unit])
    print("RESTARTED", unit, subprocess.check_output(["systemctl", "is-active", unit], text=True).strip())

in_list = ",".join("'" + n.lower().replace("'", "''") + "'" for n in REMOVE)
sql = f"""SET NOCOUNT ON;
SELECT UserID, UserName, UserType, ISNULL(UserState,1) AS S
FROM dbo.Users
WHERE LOWER(UserName) IN ({in_list});
UPDATE dbo.Users
SET UserState = 0
WHERE ISNULL(UserState,1) = 1
  AND LOWER(UserName) IN ({in_list})
  AND (UserType = N'مدير مبيعات' OR UserType LIKE N'%مبيعات%');
SELECT @@ROWCOUNT AS DisabledRows;
"""

print("=== PROVINCE DB ===")
for name in ADMINS:
    cfg_path = Path(f"/opt/qalaa-runtime/{name}/appsettings.json")
    if not cfg_path.is_file():
        print("SKIP_CFG", name)
        continue
    try:
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        server, db, user, pwd = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
    except Exception as e:
        print("CFG_ERR", name, e)
        continue
    try:
        out = subprocess.check_output(
            [SQLCMD, "-S", server, "-d", db, "-U", user, "-P", pwd, "-C", "-W", "-Q", sql],
            stderr=subprocess.STDOUT,
            text=True,
            timeout=90,
        )
        print("DB", name, db)
        print(out.strip()[-700:])
    except subprocess.CalledProcessError as e:
        print("DB_ERR", name, e.output[-500:] if e.output else e)
    except Exception as e:
        print("DB_ERR", name, e)

print("DONE")
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/remove_sm_excel_db2.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/remove_sm_excel_db2.py", timeout=600)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
