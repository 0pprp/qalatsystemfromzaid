#!/usr/bin/env python3
"""READONLY: list configured SalesManagerAccounts on production gateway."""
from __future__ import annotations
import os, sys, re
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import re, subprocess
from pathlib import Path

paths = [
  "/etc/qalaa-production/delegated-manager.env",
  "/etc/systemd/system/qalaa-salesgateway.service",
  "/etc/systemd/system/qalaa-company-salesgateway.service",
]
print("==== UNITS ====")
print(subprocess.getoutput("systemctl list-units 'qalaa*' --no-pager | grep -iE 'sales|gateway|employee' | head -40"))

print("==== FIND_ENV ====")
print(subprocess.getoutput(
  "grep -Rln 'SalesManagerAccounts' /etc/qalaa-production /etc/systemd/system /opt/qalaa-runtime 2>/dev/null | head -30"
))

for p in [
  "/etc/qalaa-production/delegated-manager.env",
  "/etc/qalaa-production/sales-gateway.env",
  "/etc/qalaa-production/besalesemployee.env",
]:
  path = Path(p)
  if not path.exists():
    print("missing", p)
    continue
  text = path.read_text(encoding="utf-8", errors="replace")
  print("==== FILE", p, "len", len(text))
  # show SM account keys without dumping unrelated secrets much
  for line in text.splitlines():
    if "SalesManagerAccount" in line or "SalesManagerAccounts" in line:
      # mask password values partially
      if "Password" in line:
        k, _, v = line.partition("=")
        v = v.strip().strip('"').strip("'")
        masked = (v[:2] + "***" + v[-2:]) if len(v) > 4 else "***"
        print(f"{k}={masked}")
      else:
        print(line)

# also systemd drop-ins
print("==== SYSTEMD_GREP ====")
print(subprocess.getoutput(
  "grep -R 'SalesManagerAccounts' /etc/systemd/system --include='*.service' --include='*.conf' 2>/dev/null | sed -E 's/(Password=).*/\\1***/' | head -60"
))
print("DONE")
'''

def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_sm_accounts.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, o, e = c.exec_command("python3 /tmp/probe_sm_accounts.py", timeout=120)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return o.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
