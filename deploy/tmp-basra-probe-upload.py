#!/usr/bin/env python3
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
NAME = sys.argv[1] if len(sys.argv) > 1 else "tmp-basra-probe-remote6.py"
LOCAL = Path(__file__).with_name(NAME)
REMOTE = f"/tmp/{NAME}"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not LOCAL.exists():
        print("missing", LOCAL)
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(LOCAL), REMOTE)
        sftp.close()
        stdin, stdout, stderr = c.exec_command(f"python3 {REMOTE}", timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write((out + ("\nSTDERR " + err[:1500] if err.strip() else "") + "\n").encode("utf-8", "replace"))
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
