#!/usr/bin/env python3
"""READONLY: SalesManagerAccounts from known env/systemd paths only."""
from __future__ import annotations
import os, sys
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import subprocess
from pathlib import Path

print("==== UNITS ====")
print(subprocess.getoutput("systemctl list-units --type=service --no-pager | grep -iE 'sales|gateway|employee|delegat' | head -40"))

print("==== LS_ENV ====")
print(subprocess.getoutput("ls -la /etc/qalaa-production 2>/dev/null | head -40"))

def show_sm(path):
    p = Path(path)
    if not p.exists():
        print("missing", path)
        return
    print("====", path)
    for line in p.read_text(encoding="utf-8", errors="replace").splitlines():
        if "SalesManagerAccount" not in line:
            continue
        if "Password" in line:
            k, _, v = line.partition("=")
            v = v.strip().strip('"').strip("'")
            masked = (v[:2] + "***" + v[-2:]) if len(v) > 4 else "***"
            print(f"{k}={masked}")
        else:
            print(line)

for p in [
  "/etc/qalaa-production/delegated-manager.env",
  "/etc/qalaa-production/sales-gateway.env",
  "/etc/qalaa-production/besalesemployee.env",
  "/etc/qalaa-production/SalesGateway.env",
]:
    show_sm(p)

print("==== SYSTEMD_FILES ====")
print(subprocess.getoutput("ls /etc/systemd/system/qalaa*sales* /etc/systemd/system/qalaa*gateway* /etc/systemd/system/qalaa*employee* 2>/dev/null"))
print(subprocess.getoutput("grep -l SalesManagerAccounts /etc/systemd/system/qalaa*.service /etc/systemd/system/qalaa*.service.d/*.conf 2>/dev/null | head"))

for svc in subprocess.getoutput("ls /etc/systemd/system/qalaa*.service 2>/dev/null").split():
    name = Path(svc).name
    if any(x in name.lower() for x in ("sales", "gateway", "employee", "delegat")):
        out = subprocess.getoutput(f"grep SalesManagerAccount {svc} 2>/dev/null | sed -E 's/(Password=).*/\\1***/'")
        if out.strip():
            print("----", name)
            print(out)

# drop-ins
print(subprocess.getoutput(
  "grep -R SalesManagerAccount /etc/systemd/system/qalaa*.service.d 2>/dev/null | sed -E 's/(Password=).*/\\1***/' | head -80"
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
        with sftp.file("/tmp/probe_sm2.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, o, e = c.exec_command("python3 /tmp/probe_sm2.py", timeout=90)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return o.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
