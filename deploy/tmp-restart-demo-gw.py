#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command(
    "systemctl restart qalaa-demo-backend; systemctl is-active qalaa-demo-backend; "
    "systemctl restart qalaa-sales-gateway qalaa-demo-sales-gateway; "
    "systemctl is-active qalaa-sales-gateway qalaa-demo-sales-gateway",
    timeout=120,
)
print(stdout.read().decode())
print(stderr.read().decode()[-500:])
c.close()
