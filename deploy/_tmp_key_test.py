#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
python3 <<'PY'
import json,glob,os
for p in glob.glob('/opt/qalaa-runtime/SalesGateway/**/*', recursive=True):
  if not p.endswith(('.json','.env','.config')): continue
  try:
    txt=open(p,encoding='utf-8',errors='ignore').read()
  except Exception:
    continue
  if 'InternalApiKey' in txt or 'X-Sales-Gateway' in txt:
    print('HIT', p)
    if p.endswith('.json'):
      try:
        d=json.loads(txt)
        print('InternalApiKey', (d.get('InternalApiKey') or '')[:6]+'...' if d.get('InternalApiKey') else None)
      except Exception as e:
        print('parse',e)
# env in systemd
import subprocess
out=subprocess.check_output(['systemctl','show','qalaa-sales-gateway','-p','Environment','--value'], text=True)
print('ENV', out[:500])
PY
KEY=$(python3 - <<'PY'
import json
for p in ['/opt/qalaa-runtime/SalesGateway/appsettings.Production.json','/opt/qalaa-runtime/SalesGateway/appsettings.json']:
  try:
    d=json.load(open(p))
    if d.get('InternalApiKey'):
      print(d['InternalApiKey']); break
  except: pass
PY
)
echo KEYLEN=${#KEY}
curl -s -o /tmp/img.bin -w '%{http_code} %{size_download}\n' -H "X-Sales-Gateway-Key: $KEY" http://127.0.0.1:5301/api/sales-manager/customer-documents/2/file
xxd -l 4 /tmp/img.bin
'''
_,o,e=c.exec_command(remote, timeout=60)
print(o.read().decode('utf-8', errors='replace'))
print(e.read().decode('utf-8', errors='replace')[-1000:])
c.close()
