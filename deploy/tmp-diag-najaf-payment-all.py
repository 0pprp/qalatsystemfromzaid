#!/usr/bin/env python3
from __future__ import annotations
import os
from pathlib import Path
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
REMOTE = Path(__file__).with_name("tmp-diag-najaf-payment-all-remote.py")
OUT = Path(__file__).with_name("_diag_najaf_all_out.txt")

def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required"); return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp(); sftp.put(str(REMOTE), "/tmp/diag_all.py"); sftp.close()
        stdin, stdout, stderr = c.exec_command("python3 /tmp/diag_all.py", timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        OUT.write_text(out + ("\nSTDERR\n" + err if err.strip() else ""), encoding="utf-8")
        print("WROTE", OUT, OUT.stat().st_size)
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
