#!/usr/bin/env python3
"""Restart all qalaa-company-* + demo backend so TOP list code loads."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import subprocess
out=subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-company-*' 'qalaa-demo-backend'")
restarted=[]
for line in out.splitlines():
    parts=line.split()
    if not parts: continue
    u=parts[0]
    name=u.removesuffix('.service')
    if 'backup' in name: continue
    subprocess.run(['systemctl','restart',name], check=False)
    active=subprocess.getoutput(f'systemctl is-active {name}')
    restarted.append(f'{name}={active}')
print('RESTARTED', len(restarted))
print('\n'.join(restarted))
# verify process start times moved
print('SAMPLE_PS')
print(subprocess.getoutput("ps -eo pid,lstart,cmd | grep 'AdminBaghdadKarkh/BE_Company' | grep -v grep"))
print(subprocess.getoutput("ps -eo pid,lstart,cmd | grep 'AdminNajaf/BE_Company' | grep -v grep"))
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/restart_company_fast_load.py", "w") as f:
                f.write(REMOTE)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/restart_company_fast_load.py", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
