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
remote = r'''
python3 <<'PY'
import json, urllib.request, urllib.error
from pathlib import Path

# InternalApiKey from env file
key = None
for line in Path('/etc/qalaa-production/delegate-najaf-sales-gateway.env').read_text().splitlines():
    if line.startswith('InternalApiKey='):
        key = line.split('=',1)[1].strip().strip('"')
# also SalesEmployee key
gw = None
for line in Path('/etc/systemd/system/qalaa-company-sharenewnajaf.service.d/sales-gateway.conf').read_text().splitlines():
    if 'GatewayKey=' in line:
        gw = line.split('GatewayKey=',1)[1].strip().strip('"')

def hit(label, key, scope='employee'):
    url = f'http://127.0.0.1:5301/api/sales-manager/stats/trend?scope={scope}&id=7&fromDate=2026-09-17&toDate=2026-09-23'
    if scope=='branch':
        url = 'http://127.0.0.1:5301/api/sales-manager/stats/trend?scope=branch&id=0&fromDate=2026-09-17&toDate=2026-09-23'
    req = urllib.request.Request(url, headers={'X-Sales-Gateway-Key': key, 'X-Sales-Manager-Name': 'probe'})
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            body = json.loads(resp.read().decode())
            print(label, scope, 'OK', 'end', body.get('endValue'), 'cust', body.get('customerCount'), 'exc', body.get('excellentCount'), 'pts', len(body.get('points') or []))
    except urllib.error.HTTPError as e:
        print(label, scope, 'HTTP', e.code)

hit('internal', key, 'employee')
hit('internal', key, 'branch')
hit('gwconf', gw, 'employee')
# what does appsettings say?
import re
cfg = Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text(encoding='utf-8', errors='replace')
for m in re.finditer(r'"(GatewayKey|InternalApiKey)"\s*:\s*"([^"]*)"', cfg):
    print('appsettings', m.group(1), 'len', len(m.group(2)), 'prefix', m.group(2)[:6])
PY
'''
_, o, e = c.exec_command(remote, timeout=90)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:500])
c.close()
