#!/usr/bin/env python3
import os
import sys

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
cmd = r"""
python3 - <<'PY'
from pathlib import Path
d=Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll').read_bytes()
print('Lifetime', b'GetEmployeeLifetimeTrendAsync' in d, 'Attach', b'AttachEmployeeLatestRatingsAsync' in d, 'size', len(d))
print('FE', any('rating-link' in p.read_text(errors='ignore') or 'التقييم' in p.name for p in Path('/opt/qalaa-runtime/AdminFrontend/assets').glob('sales-manager-employees*.js')))
import subprocess
print([p.name for p in Path('/opt/qalaa-runtime/AdminFrontend/assets').glob('*employees*')][:5])
# probe rating-trend
key=None
for line in Path('/etc/qalaa-production/delegate-najaf-sales-gateway.env').read_text().splitlines():
    if line.startswith('InternalApiKey='):
        key=line.split('=',1)[1].strip().strip('"')
import json,urllib.request
req=urllib.request.Request('http://127.0.0.1:5301/api/sales-manager/employees/7/rating-trend', headers={'X-Sales-Gateway-Key':key,'X-Sales-Manager-Name':'probe'})
with urllib.request.urlopen(req, timeout=90) as resp:
    body=json.loads(resp.read().decode())
print('trend pts', len(body.get('points') or []), 'end', body.get('endValue'), 'cust', body.get('customerCount'))
req2=urllib.request.Request('http://127.0.0.1:5301/api/sales-manager/employees', headers={'X-Sales-Gateway-Key':key,'X-Sales-Manager-Name':'probe'})
with urllib.request.urlopen(req2, timeout=90) as resp:
    rows=json.loads(resp.read().decode())
hit=next((r for r in rows if r.get('employeeId')==7 or r.get('EmployeeId')==7), rows[0] if rows else {})
print('list sample', hit.get('employeeName') or hit.get('EmployeeName'), 'latest', hit.get('latestRating') or hit.get('LatestRating'))
PY
"""
_, o, e = c.exec_command(cmd, timeout=120)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:800])
c.close()
