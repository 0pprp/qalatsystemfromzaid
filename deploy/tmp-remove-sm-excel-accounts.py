#!/usr/bin/env python3
"""Remove Excel sales-manager accounts from gateway + soft-disable in province DBs."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''#!/usr/bin/env python3
import json, os, subprocess, time, urllib.error, urllib.request
from pathlib import Path

REMOVE = ["fat", "zai", "ban", "jin", "amar1"]
REMOVE_L = {u.lower() for u in REMOVE}
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
UNITS = ["qalaa-sales-gateway", "qalaa-demo-sales-gateway"]
BASES = {
    "qalaa-sales-gateway": "http://127.0.0.1:5280/api",
    "qalaa-demo-sales-gateway": "http://127.0.0.1:5403/api",
}

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
        parts.get("user id") or parts.get("uid"),
        parts.get("password") or parts.get("pwd"),
    )

def read_accounts(unit):
    out = subprocess.check_output(
        ["systemctl", "show", unit, "-p", "Environment", "--no-pager"], text=True
    )
    env = {}
    if not out.startswith("Environment="):
        return []
    raw = out[len("Environment="):].strip()
    tokens, cur, in_q = [], "", False
    for ch in raw:
        if ch == '"':
            in_q = not in_q
            cur += ch
        elif ch == " " and not in_q:
            if cur:
                tokens.append(cur)
            cur = ""
        else:
            cur += ch
    if cur:
        tokens.append(cur)
    for t in tokens:
        if "=" not in t:
            continue
        k, v = t.split("=", 1)
        if len(v) >= 2 and v[0] == '"' and v[-1] == '"':
            v = v[1:-1]
        env[k] = v
    accounts = []
    for i in range(20):
        u = (env.get(f"SalesManagerAccounts__{i}__UserName") or "").strip()
        p = (env.get(f"SalesManagerAccounts__{i}__Password") or "").strip()
        d = (env.get(f"SalesManagerAccounts__{i}__DisplayName") or "").strip()
        if u:
            accounts.append((u, p, d or u))
    return accounts

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

print("=== GATEWAY ===")
for unit in UNITS:
    if subprocess.call(["systemctl", "cat", unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
        print("SKIP_UNIT", unit)
        continue
    before = read_accounts(unit)
    print("BEFORE", unit, [(u, d) for u, p, d in before])
    kept = [(u, p, d) for u, p, d in before if u.lower() not in REMOVE_L]
    removed = [u for u, p, d in before if u.lower() in REMOVE_L]
    print("REMOVE", unit, removed)
    print("KEEP", unit, [u for u, p, d in kept])
    write_dropin(unit, kept)

subprocess.check_call(["systemctl", "daemon-reload"])
for unit in UNITS:
    if subprocess.call(["systemctl", "cat", unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
        continue
    subprocess.check_call(["systemctl", "restart", unit])
    print("RESTARTED", unit, subprocess.check_output(["systemctl", "is-active", unit], text=True).strip())
time.sleep(4)

for unit, base in BASES.items():
    if subprocess.call(["systemctl", "cat", unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
        continue
    for u in REMOVE:
        req = urllib.request.Request(
            base + "/Auth/LoginSalesManager",
            data=json.dumps({"userName": u, "password": "wrong"}).encode(),
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=15) as resp:
                print("LOGIN_STILL_OK", unit, u, resp.status)
        except urllib.error.HTTPError as e:
            print("LOGIN_BLOCKED", unit, u, e.code)
        except Exception as e:
            print("LOGIN_CHECK", unit, u, type(e).__name__)

print("=== PROVINCE DB ===")
in_list = ",".join("N'" + n.replace("'", "''") + "'" for n in REMOVE)
sql = f"""SET NOCOUNT ON;
SELECT UserID, UserName, UserType, ISNULL(UserState,1) AS S
FROM dbo.Users
WHERE LOWER(UserName) IN ({",".join("'" + n.lower() + "'" for n in REMOVE)});
UPDATE dbo.Users
SET UserState = 0
WHERE ISNULL(UserState,1) = 1
  AND LOWER(UserName) IN ({",".join("'" + n.lower() + "'" for n in REMOVE)})
  AND (UserType = N'مدير مبيعات' OR UserType LIKE N'%مبيعات%');
SELECT @@ROWCOUNT AS DisabledRows;
"""

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
            ["sqlcmd", "-S", server, "-d", db, "-U", user, "-P", pwd, "-C", "-W", "-Q", sql],
            stderr=subprocess.STDOUT,
            text=True,
            timeout=90,
        )
        print("DB", name, db)
        print(out.strip()[-700:])
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
            with sftp.file("/tmp/remove_sm_excel_accounts.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/remove_sm_excel_accounts.py", timeout=600)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
