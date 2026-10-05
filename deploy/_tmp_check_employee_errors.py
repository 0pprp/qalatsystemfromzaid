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
journalctl -u qalaa-company-sharenewnajaf --since "10 min ago" --no-pager 2>/dev/null | grep -iE "exception|error|Employee|stats/trend|timeout|SqlException" | tail -40
echo '---'
journalctl -u qalaa-sales-gateway --since "10 min ago" --no-pager 2>/dev/null | grep -iE "director|stats/trend|employee|timeout|500|exception" | tail -30
"""
_, o, e = c.exec_command(cmd, timeout=60)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:500])
c.close()
