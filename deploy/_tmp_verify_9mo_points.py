#!/usr/bin/env python3
import json
import os
import sys
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
import json, urllib.request
from pathlib import Path
key=None
for line in Path('/etc/qalaa-production/delegate-najaf-sales-gateway.env').read_text().splitlines():
    if line.startswith('InternalApiKey='):
        key=line.split('=',1)[1].strip().strip('"')
url='http://127.0.0.1:5301/api/sales-manager/stats/trend?scope=branch&id=0&fromDate=2025-12-23&toDate=2026-09-23'
req=urllib.request.Request(url, headers={'X-Sales-Gateway-Key': key, 'X-Sales-Manager-Name':'probe'})
with urllib.request.urlopen(req, timeout=120) as resp:
    body=json.loads(resp.read().decode())
pts=body.get('points') or []
print('points', len(pts), 'from', body.get('fromDate'), 'to', body.get('toDate'), 'end', body.get('endValue'), 'cust', body.get('customerCount'))
if pts:
    print('first', pts[0], 'last', pts[-1])
PY
'''
_, o, e = c.exec_command(remote, timeout=150)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:800])
c.close()
