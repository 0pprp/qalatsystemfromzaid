#!/usr/bin/env python3
import os

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r"""
import glob, os, subprocess
print('=== nginx sales-gw ===')
subprocess.call(['bash','-lc',"grep -Rni 'sales-gw' /etc/nginx 2>/dev/null | head -40"])
print('=== gw units ===')
for u in ['qalaa-sales-gateway','qalaa-demo-sales-gateway']:
  print('---', u)
  subprocess.call(['bash','-lc', f"systemctl show {u} -p FragmentPath -p WorkingDirectory -p EnvironmentFiles -p Environment --no-pager 2>/dev/null | head -40"])
print('=== find appsettings ===')
subprocess.call(['bash','-lc',"find /opt/qalaa-runtime /opt/qalaa-demo -name 'appsettings*.json' 2>/dev/null | head -40"])
print('=== env RequireDemo ===')
subprocess.call(['bash','-lc',"grep -Rni 'RequireDemo\\|DirectorAccount' /etc/qalaa* /etc/systemd/system/qalaa*sales* 2>/dev/null | head -60"])
print('DONE')
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/inspect_gw_ng.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    i, o, e = c.exec_command("python3 /tmp/inspect_gw_ng.py", timeout=90)
    print(o.read().decode("utf-8", "replace"))
    err = e.read().decode("utf-8", "replace")
    if err.strip():
        print("STDERR", err[-800:])
finally:
    c.close()
