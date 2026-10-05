#!/usr/bin/env python3
"""Restart live qalaa-company* units after BE_Company file update."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    remote = r"""
import subprocess
out = subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-company*'")
units = []
for line in out.splitlines():
    line = line.strip()
    if not line:
        continue
    name = line.split()[0]
    if name.endswith('.service'):
        name = name[:-8]
    units.append(name)
units = sorted(set(units + ['qalaa-demo-backend']))
print('UNITS', len(units))
for u in units:
    if subprocess.run(['systemctl', 'cat', u], capture_output=True).returncode != 0:
        print('skip', u)
        continue
    r = subprocess.run(['systemctl', 'restart', u], capture_output=True, text=True)
    print(('ok' if r.returncode == 0 else 'FAIL'), u)
print('DONE')
"""
    _, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY")
    print(stdout.read().decode("utf-8", errors="replace"))
    err = stderr.read().decode("utf-8", errors="replace")
    if err.strip():
        print(err, file=sys.stderr)
    code = stdout.channel.recv_exit_status()
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
