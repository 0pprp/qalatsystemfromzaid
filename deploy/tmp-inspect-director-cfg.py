#!/usr/bin/env python3
import json
import os

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import json, os
for path in ['/opt/qalaa-runtime/SalesGateway/appsettings.json','/opt/qalaa-demo/sales-gateway/appsettings.json']:
  try:
    d=json.load(open(path,encoding='utf-8'))
  except Exception as e:
    print(path, 'ERR', e); continue
  sm=d.get('SalesManagement') or {}
  print('===', path)
  print('RequireDemoDatabase', sm.get('RequireDemoDatabase'))
  print('AllowedDemoDatabase', sm.get('AllowedDemoDatabase'))
  da=d.get('DirectorAccount') or {}
  print('DirectorAccount UserName', da.get('UserName'))
for p in ['/etc/qalaa-runtime/sales-gateway.env','/etc/qalaa-demo/sales-gateway.env']:
  if not os.path.isfile(p):
    print('MISSING', p); continue
  print('ENV', p)
  for line in open(p,encoding='utf-8',errors='replace'):
    if 'RequireDemo' in line or 'DirectorAccount' in line or 'AllowedDemo' in line:
      if 'Password' in line:
        print(' ', line.split('=',1)[0].strip()+'=<redacted>')
      else:
        print(' ', line.strip())
print('DONE')
"""


def main() -> int:
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/inspect_director_cfg.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        i, o, e = c.exec_command("python3 /tmp/inspect_director_cfg.py", timeout=60)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
