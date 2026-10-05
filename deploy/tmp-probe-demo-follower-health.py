#!/usr/bin/env python3
"""Inspect demo follower backend health on 8081/5402."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

cmd = r"""
set -e
echo '=== nginx follower conf ==='
cat /etc/nginx/sites-enabled/qalaa-demo-follower.conf
echo
echo '=== systemd follower/demo ==='
systemctl list-units --type=service --all 2>/dev/null | grep -iE 'follower|delegate|5402|demo' | head -40
echo
echo '=== listen 5402 ==='
ss -lntp | grep -E '5402|8081' || netstat -lntp 2>/dev/null | grep -E '5402|8081' || true
echo
echo '=== unit files ==='
ls /etc/systemd/system/*follower* /etc/systemd/system/*delegate* /etc/systemd/system/*demo* 2>/dev/null | head -40
echo
for u in qalaa-demo-follower qalaa-demo-delegate follower-demo BE_Delegate; do
  systemctl cat $u 2>/dev/null | head -5 && echo "--- found $u ---"
done
echo
echo '=== find working dirs ==='
ls -la /opt/qalaa-demo/ 2>/dev/null | head -30
ls -la /opt/qalaa-demo/follower-backend 2>/dev/null | head -20
ls -la /opt/qalaa-runtime/ | grep -iE 'Delegate|Follower|Demo' | head -30
echo
echo '=== local curl Lists ==='
ASYNC=$(curl -s -X POST http://127.0.0.1:5402/api/Followers/Login -H 'Content-Type: application/json' -d '{"userName":"ahmed4","password":"ahmed4"}' | python3 -c 'import sys,json; print(json.load(sys.stdin).get("asyncId",""))' 2>/dev/null || true)
echo ASYNC=$ASYNC
curl -s -o /tmp/lists.json -w 'HTTP %{http_code}\n' "http://127.0.0.1:5402/api/Followers/Lists?asyncId=$ASYNC"
head -c 400 /tmp/lists.json; echo
curl -s -o /tmp/lists2.json -w 'HTTP8081 %{http_code}\n' "http://127.0.0.1:8081/api/Followers/Lists?asyncId=$ASYNC"
head -c 400 /tmp/lists2.json; echo
echo
echo '=== journal ==='
journalctl -u qalaa-demo-follower -n 40 --no-pager 2>/dev/null || journalctl -u '*follower*' -n 20 --no-pager 2>/dev/null || true
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace"))
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[:800])
c.close()
