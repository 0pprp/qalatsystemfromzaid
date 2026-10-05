import os, paramiko
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52",username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
cmd=r'''
python3 - <<'PY'
import json,os,glob,urllib.request,subprocess
for dest in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
    print('DEST', dest, 'exists', os.path.isdir(dest))
    for p in sorted(glob.glob(dest+'/**/appsettings*.json', recursive=True))[:10]:
        print(' CFG', p)
accounts=[{"UserName":"mf1","DisplayName":"متابع رئيسي 1","Password":"Mf@12345"},{"UserName":"mf2","DisplayName":"متابع رئيسي 2","Password":"Mf@12345"}]
for dest in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
    cfg=os.path.join(dest,'appsettings.json')
    if not os.path.isfile(cfg):
        print('MISSING', cfg); continue
    with open(cfg,'r',encoding='utf-8') as fh: data=json.load(fh)
    data['MainFollowerAccounts']=accounts
    with open(cfg,'w',encoding='utf-8') as fh: json.dump(data, fh, ensure_ascii=False, indent=2)
    print('PATCHED', cfg, 'keys', list(data.keys())[-5:])
subprocess.check_call(['systemctl','restart','qalaa-sales-gateway','qalaa-demo-sales-gateway'])
import time; time.sleep(2)
# find listening
print(subprocess.getoutput("ss -lntp | grep -E 'dotnet|8080|5[0-9]{3}' | head -20"))
# read unit for env
print('WD', subprocess.getoutput('systemctl show qalaa-demo-sales-gateway -p WorkingDirectory --value'))
print('ENV', subprocess.getoutput('systemctl show qalaa-demo-sales-gateway -p Environment --value')[:500])
base='http://127.0.0.1:8080/sales-gw/api'
body=json.dumps({'userName':'mf1','password':'Mf@12345'}).encode()
req=urllib.request.Request(base+'/Auth/LoginMainFollower', data=body, headers={'Content-Type':'application/json'})
try:
    with urllib.request.urlopen(req, timeout=20) as r:
        data=json.loads(r.read().decode())
    print('LOGIN_OK', data.get('userType'), data.get('userName'))
except Exception as e:
    print('LOGIN_FAIL', e)
    # show body
    if hasattr(e,'read'):
        print(e.read().decode()[:500])
    elif hasattr(e,'file'):
        print(e.file.read().decode()[:500])
PY
'''
i,o,e=c.exec_command(cmd, timeout=90)
print(o.read().decode())
print(e.read().decode()[-1000:])
c.close()
