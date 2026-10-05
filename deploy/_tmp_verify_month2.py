#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import base64, json, subprocess, urllib.request
from pathlib import Path

dll = Path("/opt/qalaa-runtime/AdminNajaf/BE_Company.dll").read_bytes()
print("HAS_VIEW_UTF16", "View_CustomersSalesDelegate".encode("utf-16-le") in dll)
print("HAS_NOT_EXISTS_UTF16", "NOT EXISTS".encode("utf-16-le") in dll)
print("HAS_DISTINCT_UTF16", "COUNT(DISTINCT s.CustomerID)".encode("utf-16-le") in dll)
print("HAS_OLD_CUSTOMERS_C", "FROM dbo.Customers c".encode("utf-16-le") in dll)

# restart najaf to be sure
subprocess.run(["systemctl", "restart", "qalaa-company-sharenewnajaf"], check=False)
import time; time.sleep(4)
print("active", subprocess.check_output(["systemctl","is-active","qalaa-company-sharenewnajaf"], text=True).strip())

pid = subprocess.check_output(["systemctl","show","qalaa-company-sharenewnajaf","-p","MainPID","--value"], text=True).strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
key = ""
cs = ""
for item in env:
    if item.startswith(b"SalesEmployee__GatewayKey="):
        key = item.split(b"=",1)[1].decode()
    if b"ConnectionStrings__DataBaseConnection=" in item or item.startswith(b"ConnectionStrings__DataBaseConnection="):
        cs = item.split(b"=",1)[1].decode()
        print("CS_LEN", len(cs))

headers = {"X-Sales-Gateway-Key": key, "X-Sales-Manager-Name-B64": base64.b64encode(b"SalesManager").decode()}
req = urllib.request.Request("http://127.0.0.1:5301/api/sales-manager/stats/branch-month", headers=headers)
with urllib.request.urlopen(req, timeout=90) as resp:
    raw = json.loads(resp.read().decode())
    cur = raw.get("current") or raw.get("Current") or {}
    print("NEW", cur.get("newCustomersCount") or cur.get("NewCustomersCount"))
    print("ZERO", cur.get("zeroedCustomersCount") or cur.get("ZeroedCustomersCount"))
    print("YM", cur.get("yearMonth") or cur.get("YearMonth"))

# try sqlcmd if present
app = Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8", errors="replace")
import re
m = re.search(r'"DataBaseConnection"\s*:\s*"([^"]+)"', app)
print("APP_CS", bool(m))
if m:
    # parse server/db/user
    parts = dict(p.split("=",1) for p in m.group(1).split(";") if "=" in p)
    print("SERVER", parts.get("Server") or parts.get("Data Source"))
    print("DB", parts.get("Database") or parts.get("Initial Catalog"))
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/verify_month2.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/verify_month2.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
