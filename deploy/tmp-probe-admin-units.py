#!/usr/bin/env python3
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]
REMOTE = r"""
import subprocess, glob, os
print('=== units matching admin/company/qalaa ===')
print(subprocess.getoutput("systemctl list-units --type=service --all --no-legend | grep -iE 'admin|company|qalaa|backend' | head -80"))
print('=== AdminBaghdadKarkh process ===')
print(subprocess.getoutput("ps aux | grep -i 'AdminBaghdadKarkh\\|BE_Company' | grep -v grep | head -20"))
print('=== working dirs ===')
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*'))[:5]:
    print(d, 'dll', os.path.isfile(os.path.join(d,'BE_Company.dll')))
print('=== how karkh started ===')
print(subprocess.getoutput("systemctl list-units --type=service --all --no-legend | head -5"))
print(subprocess.getoutput("ls /etc/systemd/system | grep -iE 'qalaa|admin|company' | head -40"))
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
with c.open_sftp() as sftp:
    with sftp.file("/tmp/probe_admin_units.py", "w") as f:
        f.write(REMOTE)
stdin, stdout, stderr = c.exec_command("python3 /tmp/probe_admin_units.py", timeout=60)
print(stdout.read().decode("utf-8", "replace"))
print(stderr.read().decode("utf-8", "replace")[-1000:])
c.close()
