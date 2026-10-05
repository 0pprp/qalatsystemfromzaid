#!/usr/bin/env python3
import os, sys, json
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
python3 <<'PY'
import json, urllib.request, ssl
# load gateway appsettings for internal key / how branch proxy auths
for p in ['/opt/qalaa-runtime/SalesGateway/appsettings.json','/opt/qalaa-runtime/sales-gateway/appsettings.json','/opt/qalaa-demo/sales-gateway/appsettings.json']:
  try:
    d=json.load(open(p))
    print('CFG', p)
    print(json.dumps({k:d.get(k) for k in d if 'Key' in k or 'Internal' in k or 'Branch' in k or 'Sales' in k}, ensure_ascii=False)[:800])
  except Exception as e:
    pass

# Direct branch call as gateway would - find header from BranchProxyService config
najaf=json.load(open('/opt/qalaa-runtime/AdminNajaf/appsettings.json'))
print('Najaftokens?', list(najaf.keys())[:20])
PY

# Use curl to local with a forged approach: call through nginx host with existing cookie? 
# Simplest: curl file bytes via path proof + simulate ReadFileAsync path resolution
python3 <<'PY'
from pathlib import Path
root=Path('/opt/qalaa-runtime/AdminNajaf/App_Data')
for key in ['sales/customer-docs/2.jpg','sales/2/shop.jpg']:
  p=root/key
  data=p.read_bytes()[:16]
  print(key, 'size', p.stat().st_size, 'magic', data[:3].hex(), 'jpeg' if data[:2]==b'\xff\xd8' else 'other')
PY

# Hit company API using the same internal header the gateway uses - grep dll/config
grep -R "InternalGateway\|X-Gateway\|X-Internal\|ManagerKey\|BranchProxy" /opt/qalaa-runtime/SalesGateway/appsettings*.json 2>/dev/null | head -20
grep -R "InternalGateway\|X-Gateway\|AcceptGateway\|SalesManager" /opt/qalaa-runtime/AdminNajaf/appsettings*.json 2>/dev/null | head -20
'''
_,o,e=c.exec_command(remote, timeout=60)
print(o.read().decode('utf-8', errors='replace'))
print(e.read().decode('utf-8', errors='replace')[-1000:])
c.close()
