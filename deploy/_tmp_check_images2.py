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
    # Recent image/profile traffic
    "grep -E 'customer-documents|shop-image|/profile' /var/log/nginx/access.log | tail -80",
    # Test local branch file endpoint for an existing doc
    "ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs | head -5",
    # Check if doc id 123 exists in DB and on disk; hit local API without auth may 401
    "curl -s -o /tmp/doc123.bin -w '%{http_code} %{content_type} %{size_download}\\n' http://127.0.0.1:5301/api/sales-manager/customer-documents/123/file",
    "curl -s -o /tmp/shop64.bin -w '%{http_code} %{content_type} %{size_download}\\n' http://127.0.0.1:5301/api/sales-manager/sales/64/shop-image",
    "xxd /tmp/doc123.bin 2>/dev/null | head -2; xxd /tmp/shop64.bin 2>/dev/null | head -2",
    # Check ReadFile / 404 body
    "curl -s http://127.0.0.1:5301/api/sales-manager/customer-documents/2/file | head -c 400; echo",
    "curl -s http://127.0.0.1:5301/api/sales-manager/customer-documents/123/file | head -c 200; echo",
]

for cmd in cmds:
    print("====", cmd[:120])
    _, o, e = c.exec_command(cmd, timeout=60)
    print(o.read().decode("utf-8", errors="replace")[-4000:])
    err = e.read().decode("utf-8", errors="replace")
    if err.strip():
        print("ERR", err[-500:])

c.close()
