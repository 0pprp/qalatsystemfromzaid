#!/usr/bin/env python3
import json
import os
import sys
import urllib.request

import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=40, allow_agent=False, look_for_keys=False)
remote = r'''
python3 <<'PY'
import json,urllib.request
from pathlib import Path
key=None
for line in Path('/etc/qalaa-production/delegate-najaf-sales-gateway.env').read_text().splitlines():
    if line.startswith('InternalApiKey='):
        key=line.split('=',1)[1].strip().strip('"')
h={'X-Sales-Gateway-Key':key,'X-Sales-Manager-Name':'probe'}
def get(url):
    req=urllib.request.Request(url, headers=h)
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read().decode())
rows=get('http://127.0.0.1:5301/api/sales-manager/employees')
# find محمد حمود or any with rating
for r in rows:
    name=r.get('employeeName') or r.get('EmployeeName') or ''
    if 'حمود' in name or 'دودان' in name:
        eid=r.get('employeeId') or r.get('EmployeeId')
        print('LIST', name, 'latest', r.get('latestRating') or r.get('LatestRating'), 'cust', r.get('ratedCustomerCount') or r.get('RatedCustomerCount'))
        t=get(f'http://127.0.0.1:5301/api/sales-manager/employees/{eid}/rating-trend')
        print('TREND end', t.get('endValue'), 'cust', t.get('customerCount'), 'pts', len(t.get('points') or []), 'exc', t.get('excellentCount'), 'weak', t.get('weakCount'))
        break
else:
    # fallback id 7
    r=next((x for x in rows if (x.get('employeeId') or x.get('EmployeeId'))==7), rows[0] if rows else {})
    eid=r.get('employeeId') or r.get('EmployeeId')
    print('LIST fallback', r.get('employeeName'), r.get('latestRating'), r.get('ratedCustomerCount'))
    t=get(f'http://127.0.0.1:5301/api/sales-manager/employees/{eid}/rating-trend')
    print('TREND', t.get('endValue'), t.get('customerCount'), len(t.get('points') or []))
PY
'''
_,o,e=c.exec_command(remote, timeout=150)
print(o.read().decode('utf-8','replace'))
print(e.read().decode('utf-8','replace')[:500])
c.close()
