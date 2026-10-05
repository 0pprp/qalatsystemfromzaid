#!/usr/bin/env python3
import json
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
python3 <<'PY'
import json,os,glob
for p in [
 '/opt/qalaa-runtime/SalesGateway/appsettings.json',
 '/opt/qalaa-runtime/SalesGateway/appsettings.Production.json',
 '/opt/qalaa-demo/sales-gateway/appsettings.json',
]:
  if not os.path.isfile(p):
    print('MISSING', p); continue
  j=json.load(open(p,encoding='utf-8'))
  print('====', p)
  print('SalesManagerAccount', j.get('SalesManagerAccount'))
  print('SalesManagerAccounts', j.get('SalesManagerAccounts'))
  print('EnvironmentVariables override check via systemd:')
print('--- systemd env ---')
import subprocess
for u in ['qalaa-sales-gateway','qalaa-demo-sales-gateway']:
  out=subprocess.check_output(['systemctl','show',u,'-p','Environment','-p','EnvironmentFiles','-p','WorkingDirectory'],text=True)
  print(u, out)
PY
# redact passwords when printing env file keys only
for f in /etc/qalaa-demo/sales-gateway.env /etc/qalaa-production/sales-gateway.env; do
  [ -f "$f" ] || continue
  echo FILE $f
  grep -E '^[A-Za-z0-9_]+=' "$f" | cut -d= -f1
done
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=60)
print(stdout.read().decode("utf-8", "replace"))
print(stderr.read().decode("utf-8", "replace")[:500])
c.close()
