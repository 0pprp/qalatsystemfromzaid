#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, re, subprocess, urllib.request, base64
from pathlib import Path

# Use company API month endpoint via gateway key after we change code — for now probe SQL columns via tsql if available
pid = subprocess.check_output(["systemctl", "show", "qalaa-company-sharenewnajaf", "-p", "MainPID", "--value"], text=True).strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
cs = ""
for item in env:
    s = item.decode("utf-8", "replace")
    if "ConnectionStrings__Sales" in s or s.startswith("ConnectionStrings__"):
        print(s[:160])
    if "Server=" in s and "Database=" in s:
        print("CS_CAND", s.split("=",1)[0], len(s))

app = Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8", errors="replace")
data = json.loads(app)
cs_map = data.get("ConnectionStrings") or {}
for k,v in cs_map.items():
    print("APP_CS", k, str(v)[:100])

# compare counts via local API after reading current branch-month
key=""
for item in env:
    if item.startswith(b"SalesEmployee__GatewayKey="):
        key=item.split(b"=",1)[1].decode()
headers={"X-Sales-Gateway-Key":key,"X-Sales-Manager-Name-B64":base64.b64encode(b"SalesManager").decode()}
req=urllib.request.Request("http://127.0.0.1:5301/api/sales-manager/stats/branch-month", headers=headers)
with urllib.request.urlopen(req, timeout=60) as resp:
    raw=json.loads(resp.read().decode())
    cur=raw.get("current") or raw.get("Current") or {}
    print("CURRENT_NEW", cur.get("newCustomersCount") or cur.get("NewCustomersCount"))
    print("CURRENT_ZERO", cur.get("zeroedCustomersCount") or cur.get("ZeroedCustomersCount"))
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_month.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_month.py", timeout=90)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
