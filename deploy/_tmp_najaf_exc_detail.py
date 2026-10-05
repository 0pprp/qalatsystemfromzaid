#!/usr/bin/env python3
import os
import sys

import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=40,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
# status
systemctl is-active qalaa-company-sharenewnajaf
# pull broader exception text around employee time
journalctl -u qalaa-company-sharenewnajaf --since "30 min ago" --no-pager 2>/dev/null | grep -nE "BadImage|NullReference|InvalidOperation|SqlException|EmployeeTrend|GetFacts|stats/trend|Unhandled|fail:" | tail -80
echo '==== FULL SLICE ===='
journalctl -u qalaa-company-sharenewnajaf --since "30 min ago" --no-pager 2>/dev/null | sed -n '1,120p' | tail -80
"""
_, o, e = c.exec_command(cmd, timeout=90)
print(o.read().decode("utf-8", "replace")[-12000:])
c.close()
