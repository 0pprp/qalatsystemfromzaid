#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import subprocess
from pathlib import Path

# locate any 71.jpg / customer-docs backups
cmds = [
    "find /opt/qalaa-runtime/AdminNajaf -name '71.jpg' 2>/dev/null | head",
    "find /opt/qalaa-runtime -path '*customer-docs/71.*' 2>/dev/null | head",
    "find /opt -name '71.jpg' 2>/dev/null | head",
    "ls -la /opt/qalaa-runtime/AdminNajaf/App_Data/sales/ 2>/dev/null",
    "ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs 2>/dev/null | wc -l",
    "ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs 2>/dev/null | sort -n | tail -20",
    "ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs 2>/dev/null | sort -n | head -20",
    # sale docs?
    "ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/ 2>/dev/null",
    "find /opt/qalaa-runtime/AdminNajaf/App_Data -iname '*hamza*' 2>/dev/null | head",
    "find /backup /var/backups /root -iname '71.jpg' 2>/dev/null | head",
    "find /opt/qalaa-runtime -type d -name 'customer-docs*' 2>/dev/null",
]
for c in cmds:
    print(">>", c)
    try:
        out = subprocess.check_output(c, shell=True, text=True, errors="replace", timeout=60)
        print(out[:1500])
    except Exception as ex:
        print("ERR", ex)

# highest id on disk vs missing around 71
docs = Path("/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs")
ids = []
for p in docs.iterdir():
    stem = p.stem
    if stem.isdigit():
        ids.append(int(stem))
ids.sort()
print("COUNT", len(ids), "MIN", ids[0] if ids else None, "MAX", ids[-1] if ids else None)
missing = [i for i in range(71,75) if i not in ids]
print("MISSING_71_74", missing)
# nearby present?
for i in range(60, 90):
    if i in ids:
        print("HAVE", i)
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc8.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc8.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
