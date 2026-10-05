#!/usr/bin/env python3
"""Restart Admin backends after DLL sync (fast-load deploy follow-up)."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import subprocess
units=subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-admin*' 'qalaa-demo-backend'").splitlines()
restarted=[]
for line in units:
    u=line.split()[0] if line.strip() else ''
    if not u: continue
    name=u.removesuffix('.service')
    if 'backup' in name: continue
    subprocess.run(['systemctl','restart',name], check=False)
    active=subprocess.getoutput(f'systemctl is-active {name}')
    restarted.append(f'{name}={active}')
print('RESTARTED', len(restarted))
print('\n'.join(restarted[:50]))
for u in ['qalaa-sales-gateway','qalaa-demo-sales-gateway']:
    print(u, subprocess.getoutput(f'systemctl is-active {u}'))
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
            with sftp.file("/tmp/restart_admin_fast_load.py", "w") as f:
                f.write(REMOTE)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/restart_admin_fast_load.py", timeout=300)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
