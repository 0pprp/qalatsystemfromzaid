#!/usr/bin/env python3
import os
import paramiko

SSH_PASS = os.environ.get("SSH_PASS", "")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=SSH_PASS,
    timeout=40,
    allow_agent=False,
    look_for_keys=False,
)
remote = r"""
set -e
ls -la /tmp/fe_rating_*.tgz | tail -3
TGZ=$(ls -1t /tmp/fe_rating_*.tgz | head -1)
echo USING $TGZ
rm -rf /tmp/fe_rating_extract_fix
mkdir -p /tmp/fe_rating_extract_fix
tar -xzf "$TGZ" -C /tmp/fe_rating_extract_fix
rsync -a /tmp/fe_rating_extract_fix/ /opt/qalaa-runtime/AdminFrontend/
stat -c "%y" /opt/qalaa-runtime/AdminFrontend/index.html
grep -R "نقاط زبائن" /opt/qalaa-runtime/AdminFrontend/assets 2>/dev/null | head -2 || true
systemctl reload nginx 2>/dev/null || true
echo FE_OK
"""
_, o, e = c.exec_command(remote, timeout=120)
print(o.read().decode("utf-8", "replace"))
err = e.read().decode("utf-8", "replace")
if err.strip():
    print("ERR", err[:800])
print("exit", o.channel.recv_exit_status())
c.close()
