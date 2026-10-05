import os, json, paramiko
from pathlib import Path
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('169.58.236.52', username='root', password=os.environ['SSH_PASS'], timeout=60, allow_agent=False, look_for_keys=False)
REMOTE=r'''
import json, os, re, subprocess
from pathlib import Path

# list all Delegate* dirs with dll
print('=== delegate runtimes ===')
for root in ['/opt/qalaa-runtime','/opt/qalaa-demo']:
  if not os.path.isdir(root): continue
  for name in sorted(os.listdir(root)):
    p=os.path.join(root,name)
    dll=os.path.join(p,'BE_DelegateWebApplication.dll')
    if os.path.isfile(dll):
      cfg=os.path.join(p,'appsettings.json')
      has=os.path.isfile(cfg)
      size=os.path.getsize(cfg) if has else 0
      print(f'{name} cfg={has} size={size}')

# show najaf appsettings full structure keys + SalesGateway from env/unit
p='/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json'
print('Najaf size', os.path.getsize(p))
d=json.loads(Path(p).read_text(encoding='utf-8'))
print('keys', list(d.keys()))
print('has CS', 'ConnectionStrings' in d)
# Production overlay?
for f in ['appsettings.Production.json','appsettings.Development.json']:
  fp=os.path.join('/opt/qalaa-runtime/DelegateHaiderNajafLast', f)
  print(f, 'exists', os.path.isfile(fp), 'size', os.path.getsize(fp) if os.path.isfile(fp) else 0)
  if os.path.isfile(fp):
    dd=json.loads(Path(fp).read_text(encoding='utf-8'))
    print(' ', list(dd.keys()))
    print('  SalesGateway', dd.get('SalesGateway'))
    print('  Branch', dd.get('Branch'))
    print('  InternalApiKey', 'SET' if dd.get('InternalApiKey') else None)

# systemd env for najaf delegate
out=subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-delegate*'")
print('=== units ===')
for line in out.splitlines():
  if 'najaf' in line.lower() or 'Najaf' in line:
    print(line)
    name=line.split()[0].removesuffix('.service')
    print(subprocess.getoutput(f'systemctl cat {name} | head -80'))

# Find working SalesGateway from any delegate
print('=== any SalesGateway BaseUrl ===')
for root,dirs,files in os.walk('/opt/qalaa-runtime'):
  if 'appsettings.json' in files and 'Delegate' in root:
    fp=os.path.join(root,'appsettings.json')
    try:
      d=json.loads(Path(fp).read_text(encoding='utf-8'))
    except: continue
    gw=d.get('SalesGateway') or {}
    if gw.get('BaseUrl') or d.get('InternalApiKey'):
      print(root, gw.get('BaseUrl'), 'key', bool(d.get('InternalApiKey') or gw.get('ApiKey')), 'branch', (d.get('Branch') or {}))
'''
_,o,e=c.exec_command("python3 - <<'PY'\n"+REMOTE+"\nPY")
print(o.read().decode('utf-8','replace').encode('ascii','replace').decode('ascii'))
print(e.read().decode('utf-8','replace')[:500])
c.close()
