#!/usr/bin/env python3
"""Upload and run demo follower AsyncID collision fix."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
REMOTE_PY = Path(__file__).with_name("tmp-fix-demo-follower-asyncid-remote.py")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(REMOTE_PY), "/tmp/fix_demo_follower_async.py")
        sftp.close()
        stdin, stdout, stderr = c.exec_command("python3 /tmp/fix_demo_follower_async.py", timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:1000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
