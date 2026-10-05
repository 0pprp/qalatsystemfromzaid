#!/usr/bin/env python3
"""Inspect live delegate mobile-update row + APKs on production."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    remote = r"""
import json, os, urllib.request
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/delegate?currentVersionCode=1', timeout=20) as r:
    print(json.dumps(json.loads(r.read().decode()), ensure_ascii=False, indent=2))
print('APKS')
base = '/opt/qalaa-updates/delegate'
if os.path.isdir(base):
    for f in sorted(os.listdir(base)):
        p = os.path.join(base, f)
        if os.path.isfile(p) or os.path.islink(p):
            try:
                print(f, os.path.getsize(p))
            except OSError as e:
                print(f, e)
else:
    print('missing', base)
"""
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/check_delegate_update.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/check_delegate_update.py", timeout=60)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
