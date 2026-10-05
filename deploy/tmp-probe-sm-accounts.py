#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=30,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
set +e
echo '=== units ==='
systemctl list-units --type=service --all --no-legend | grep -i sales-gateway | head -20
echo '=== env files ==='
ls -la /etc/qalaa-production/*gateway* /etc/qalaa-demo/*gateway* 2>/dev/null
echo '=== grep SalesManager in envs ==='
grep -RIn --include='*.env' 'SalesManager' /etc/qalaa-production /etc/qalaa-demo 2>/dev/null | head -60
echo '=== appsettings paths ==='
find /opt/qalaa-runtime /opt/qalaa-demo -maxdepth 3 -iname '*gateway*' -type d 2>/dev/null | head -30
for d in /opt/qalaa-runtime/SalesGateway /opt/qalaa-demo/sales-gateway /opt/qalaa-runtime/BE_SalesEmployee; do
  if [ -f "$d/appsettings.json" ]; then
    echo FILE $d/appsettings.json
    python3 - <<PY
import json
p="$d/appsettings.json"
j=json.load(open(p,encoding='utf-8'))
print('primary', j.get('SalesManagerAccount'))
print('extra', j.get('SalesManagerAccounts'))
PY
  fi
done
echo '=== WD ==='
systemctl show qalaa-sales-gateway -p WorkingDirectory --value 2>/dev/null
systemctl show qalaa-demo-sales-gateway -p WorkingDirectory --value 2>/dev/null
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=60)
print(stdout.read().decode("utf-8", "replace")[-12000:])
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("ERR", err[:800])
c.close()
