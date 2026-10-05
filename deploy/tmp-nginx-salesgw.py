#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)
cmd = r"""
echo COMPANY_SALES_GW
sed -n '390,430p' /etc/nginx/sites-enabled/qalaa-company.conf
echo DEMO_SALES_GW
sed -n '1,60p' /etc/nginx/sites-enabled/qalaa-demo.conf
echo LISTEN_COMPANY
grep -n listen /etc/nginx/sites-enabled/qalaa-company.conf | head -15
echo LISTEN_DEMO
grep -n listen /etc/nginx/sites-enabled/qalaa-demo.conf | head -15
echo PROD_ENV_DIR
ls /etc/qalaa-production/ 2>/dev/null
grep -n RequireDemo /etc/systemd/system/qalaa-sales-gateway.service /etc/systemd/system/qalaa-sales-gateway.service.d/* 2>/dev/null | head -20
"""
i, o, e = c.exec_command(cmd, timeout=30)
print(o.read().decode("utf-8", "replace").encode("ascii", "replace").decode())
c.close()
