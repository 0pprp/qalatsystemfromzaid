#!/usr/bin/env python3
"""READ-ONLY: Basra (AdminBasra + Alanwar) payment intake for ALL lists. No writes."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
REMOTE_PY = Path(__file__).with_name("_remote_basra_payments_ro.py")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not REMOTE_PY.exists():
        print("missing", REMOTE_PY)
        return 2

    body = REMOTE_PY.read_text(encoding="utf-8")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/basra_payments_ro_all.py", "w") as f:
                f.write(body)
        _, o, e = c.exec_command("python3 /tmp/basra_payments_ro_all.py", timeout=300)
        out = o.read().decode("utf-8", "replace")
        err = e.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        print(out)
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
