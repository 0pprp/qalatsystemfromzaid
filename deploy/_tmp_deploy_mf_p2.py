from __future__ import annotations
import json, os, shutil, subprocess, tarfile, time
from pathlib import Path
import paramiko
HOST="169.58.236.52"
ROOT=Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")
PROJ=ROOT/"BE_SalesEmployee"
PUB=ROOT/"_publish_gw_mf_p2"
SKIP={"appsettings.json","appsettings.Development.json","appsettings.Production.json"}
if PUB.exists(): shutil.rmtree(PUB)
PUB.mkdir(parents=True)
print("publish...", flush=True)
r=subprocess.run(["dotnet","publish",str(PROJ/"BE_SalesEmployee.csproj"),"-c","Release","-o",str(PUB),"--nologo"],capture_output=True,text=True)
if r.returncode: print(r.stdout[-2000:]); print(r.stderr[-2000:]); raise SystemExit(r.returncode)
stamp=time.strftime("%Y%m%d_%H%M%S")
tar_path=ROOT/f"_publish_gw_mf_p2_{stamp}.tgz"
with tarfile.open(tar_path,"w:gz") as tar:
    for p in PUB.rglob("*"):
        if p.is_file() and p.name not in SKIP:
            tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\","/"))
print("tar", tar_path.stat().st_size//1024, "KB", flush=True)
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
sftp=c.open_sftp(); sftp.put(str(tar_path),f"/tmp/gw_mf_p2_{stamp}.tgz"); sftp.close()
remote=f"""
import json,os,shutil,tarfile,subprocess,urllib.request,time
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/gw_mf_p2_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True); os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/gw_mf_p2_{{stamp}}.tgz').extractall(extract)
for dest in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
    if not os.path.isfile(os.path.join(dest,'BE_SalesEmployee.dll')): continue
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f); rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel); os.makedirs(os.path.dirname(target), exist_ok=True); shutil.copy2(full, target)
    print('UPDATED', dest)
subprocess.check_call(['systemctl','restart','qalaa-demo-sales-gateway','qalaa-sales-gateway'])
time.sleep(3)
base='http://127.0.0.1:5403/api'

def post(path, body, token=None):
    headers={{'Content-Type':'application/json'}}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, data=json.dumps(body).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode())

def get(path, token):
    req=urllib.request.Request(base+path, headers={{'Authorization':'Bearer '+token}})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())

mf=post('/Auth/LoginMainFollower', {{'userName':'mf1','password':'Mf@12345'}})
dm=post('/Auth/LoginDelegatedManager', {{'userName':'dm','password':'dm'}})
committee=post('/delegated-manager/committees', {{'name':'لجنة مرحلة2','memberUserNames':['mf1','mf2']}}, dm['token'])
# use Najaf DEMO city value from lab cities if possible
city='1'  # LabCities demo value
try:
    mission=post('/delegated-manager/committees/missions', {{'committeeId':committee['id'],'cityValues':[city,'DatabaseCompanyNajaf_DEMO'],'note':'p2'}}, dm['token'])
except Exception as e:
    mission=post('/delegated-manager/committees/missions', {{'committeeId':committee['id'],'cityValues':['DatabaseCompanyBasra'],'note':'p2'}}, dm['token'])
    city=mission['cityValues'][0]
print('MISSION', mission['id'], mission['cityValues'])
# try sales list
for cval in mission['cityValues']:
    try:
        sales=get(f"/main-follower/missions/{{mission['id']}}/cities/{{cval}}/sales", mf['token'])
        print('SALES', cval, type(sales).__name__, len(sales) if isinstance(sales,list) else sales)
        if isinstance(sales,list) and sales:
            city=cval
            sale=sales[0]
            sale_id=sale.get('saleId') or sale.get('SaleId') or sale.get('id') or sale.get('Id')
            name=sale.get('customerName') or sale.get('CustomerName') or 'زبون'
            break
    except Exception as ex:
        print('SALES_FAIL', cval, ex)
        sale_id=101
        name='اختبار'
# submit review score 8 with note
rev=post('/main-follower/sale-reviews', {{
  'missionId': mission['id'], 'cityValue': city, 'saleId': int(sale_id or 101),
  'customerName': name, 'score': 8, 'note': 'مراقبة لجنة جيدة'
}}, mf['token'])
print('REVIEW', rev.get('id'), rev.get('score'))
# reject without note
import urllib.error
try:
    post('/main-follower/sale-reviews', {{
      'missionId': mission['id'], 'cityValue': city, 'saleId': 999, 'score': 9, 'note': ''
    }}, mf['token'])
    print('UNEXPECTED_OK')
except urllib.error.HTTPError as e:
    body=e.read().decode()
    print('REJECT_OK', e.code, body[:120])
rows=get(f"/main-follower/missions/{{mission['id']}}/sale-reviews", mf['token'])
print('LIST_REVIEWS', len(rows))
print('DONE')
"""
with c.open_sftp() as s2:
    with s2.file('/tmp/deploy_mf_p2.py','w') as f: f.write(remote)
i,o,e=c.exec_command('python3 /tmp/deploy_mf_p2.py', timeout=300)
print(o.read().decode('utf-8','replace'))
err=e.read().decode('utf-8','replace')
if err.strip(): print('STDERR', err[-1500:])
print('exit', o.channel.recv_exit_status())
c.close()
