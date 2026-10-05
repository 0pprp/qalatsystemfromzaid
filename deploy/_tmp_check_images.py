#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=60,
    allow_agent=False,
    look_for_keys=False,
)

cmds = [
    "ls /opt/qalaa-runtime/AdminNajaf/App_Data 2>/dev/null | head -30",
    "find /opt/qalaa-runtime/AdminNajaf/App_Data -maxdepth 3 -type d 2>/dev/null | head -40",
    "find /opt/qalaa-runtime/AdminNajaf/App_Data -type f \\( -name '*.jpg' -o -name '*.jpeg' -o -name '*.png' \\) 2>/dev/null | head -15",
    "journalctl -u qalaa-company-sharenewnajaf --since '30 min ago' --no-pager 2>/dev/null | tail -80",
    "journalctl -u qalaa-sales-gateway --since '30 min ago' --no-pager 2>/dev/null | tail -80",
    # ValueTuple pattern issue? check if GetFile returns correctly - look at recent 500s
    "grep -R \"customer-documents\" /var/log/nginx/*.log 2>/dev/null | tail -20; ls /var/log/nginx 2>/dev/null | head",
]

for cmd in cmds:
    print("====", cmd[:100])
    _, o, e = c.exec_command(cmd, timeout=90)
    out = o.read().decode("utf-8", errors="replace")
    err = e.read().decode("utf-8", errors="replace")
    print(out[-3000:] if out else "(empty)")
    if err.strip():
        print("ERR", err[-800:])

c.close()
