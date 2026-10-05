#!/usr/bin/env python3
import os
import sys
import urllib.error
import urllib.request

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
remote = r"""
python3 <<'PY'
import os, subprocess, urllib.request, urllib.error
from pathlib import Path

# collect candidate keys
cands = []
env = subprocess.getoutput('systemctl show qalaa-company-sharenewnajaf -p Environment --value')
for part in env.split():
    if 'GatewayKey=' in part or 'gateway' in part.lower() and 'Key=' in part:
        cands.append(('systemd', part.split('=',1)[-1][:8], part.split('=',1)[-1]))
for p in Path('/etc/qalaa-production').glob('*.env'):
    for line in p.read_text(errors='replace').splitlines():
        if 'Key' in line and '=' in line and not line.strip().startswith('#'):
            k,v = line.split('=',1)
            cands.append((str(p), k, v.strip().strip('"')))
for p in Path('/etc/systemd/system/qalaa-company-sharenewnajaf.service.d').glob('*.conf'):
    for line in p.read_text(errors='replace').splitlines():
        if 'GatewayKey=' in line:
            v = line.split('GatewayKey=',1)[1].strip().strip('"')
            cands.append((str(p), 'GatewayKey', v))

print('candidates', len(cands))
for src, name, key in cands:
    print('try', src, name, 'len', len(key), 'prefix', key[:6])
    url = 'http://127.0.0.1:5301/api/sales-manager/stats/trend?scope=employee&id=7&fromDate=2026-09-17&toDate=2026-09-23'
    req = urllib.request.Request(url, headers={
        'X-Sales-Gateway-Key': key,
        'X-Sales-Manager-Name': 'probe',
    })
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            body = resp.read().decode('utf-8','replace')
            print('OK', resp.status, body[:300])
            break
    except urllib.error.HTTPError as e:
        print('HTTP', e.code)
    except Exception as e:
        print('ERR', e)
PY
"""
_, o, e = c.exec_command(remote, timeout=120)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:1000])
c.close()
