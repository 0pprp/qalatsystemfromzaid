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

for base in [
    "/opt/qalaa-runtime/AdminNajaf/App_Data/sales/24",
    "/opt/qalaa-runtime/AdminNajaf.bak-legal-20260919_182337/App_Data/sales/customer-docs",
    "/opt/qalaa-runtime/AdminNajaf.bak-followerlists-20260919_191309/App_Data/sales/customer-docs",
]:
    p = Path(base)
    print("==", base, "exists", p.exists())
    if p.is_dir():
        files = sorted(p.iterdir(), key=lambda x: x.name)[:40]
        for f in files:
            print(" ", f.name, f.stat().st_size)
        for i in [71,72,73,74]:
            hits = list(p.glob(f"{i}.*")) if p.name=="customer-docs" or True else []
            if p.name == "24":
                print(" sale24 all:", [x.name for x in p.iterdir()])
                break
            print(" ", i, hits)

# restore candidates from najaf backups
for bak in [
    "/opt/qalaa-runtime/AdminNajaf.bak-legal-20260919_182337/App_Data/sales/customer-docs",
    "/opt/qalaa-runtime/AdminNajaf.bak-followerlists-20260919_191309/App_Data/sales/customer-docs",
]:
    for i in [71,72,73,74]:
        src = Path(bak) / f"{i}.jpg"
        print("BAK", bak.split("/")[3] if False else Path(bak).parts[3], i, src.exists(), src.stat().st_size if src.exists() else 0)

# how many missing between 2-243?
docs = Path("/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs")
have = {int(p.stem) for p in docs.iterdir() if p.stem.isdigit()}
missing = [i for i in range(min(have), max(have)+1) if i not in have]
print("MISSING_COUNT", len(missing), "sample", missing[:40], "...", missing[-20:])

# compare bak-legal vs live for 71-74
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc9.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc9.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
