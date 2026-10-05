#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import os, glob, subprocess
print('=== units ===')
print(subprocess.getoutput("systemctl list-units --type=service --all | grep -iE 'sales|gateway|qalaa' | head -40"))
print('=== paths ===')
for p in [
  '/opt/qalaa-runtime/SalesGateway',
  '/opt/qalaa-runtime',
  '/etc/qalaa-production',
  '/opt/qalaa-demo',
]:
  print(p, 'EXISTS' if os.path.exists(p) else 'MISS')
  if os.path.isdir(p):
    print(subprocess.getoutput(f'ls -la {p} | head -30'))
print('=== appsettings hits ===')
print(subprocess.getoutput("find /opt /etc -name 'appsettings*.json' 2>/dev/null | head -40"))
print('=== env files ===')
print(subprocess.getoutput("find /etc /opt -name '*sales*gateway*' -o -name '*SalesGateway*' 2>/dev/null | head -40"))
print('=== nginx tutorial ===')
print(subprocess.getoutput("grep -R 'mobile-tutorial' /etc/nginx 2>/dev/null | head -20"))
print('=== listen 5280 ===')
print(subprocess.getoutput("ss -lntp | grep -E '5280|5000|8080' | head -20"))
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    with c.open_sftp() as sftp:
        with sftp.file("/tmp/probe_tutorial.py", "w") as f:
            f.write(REMOTE)
    _, out, err = c.exec_command("python3 /tmp/probe_tutorial.py", timeout=120)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    from pathlib import Path
    Path(__file__).with_name("_probe_tutorial_out.txt").write_text(text + e, encoding="utf-8")
    print(text)
    if e.strip():
        print("ERR", e[-1000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
