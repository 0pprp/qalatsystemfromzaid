from __future__ import annotations
import json, os, shutil, subprocess, tarfile, time
from pathlib import Path
import paramiko
HOST="169.58.236.52"
ROOT=Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")
PROJ=ROOT/"BE_SalesEmployee"
PUB=ROOT/"_publish_gw_mf_phase1"
SKIP={"appsettings.json","appsettings.Development.json","appsettings.Production.json"}
if PUB.exists(): shutil.rmtree(PUB)
PUB.mkdir(parents=True)
print("publish...", flush=True)
r=subprocess.run(["dotnet","publish",str(PROJ/"BE_SalesEmployee.csproj"),"-c","Release","-o",str(PUB),"--nologo"],capture_output=True,text=True)
if r.returncode: print(r.stdout[-2000:]); print(r.stderr[-2000:]); raise SystemExit(r.returncode)
stamp=time.strftime("%Y%m%d_%H%M%S")
tar_path=ROOT/f"_publish_gw_mf_{stamp}.tgz"
with tarfile.open(tar_path,"w:gz") as tar:
    for p in PUB.rglob("*"):
        if p.is_file() and p.name not in SKIP:
            tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\","/"))
print("tar ok", tar_path.stat().st_size//1024, "KB", flush=True)
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
sftp=c.open_sftp(); sftp.put(str(tar_path),f"/tmp/gw_mf_{stamp}.tgz"); sftp.close()
remote=f"""
import json,os,shutil,tarfile,subprocess
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/gw_mf_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True); os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/gw_mf_{{stamp}}.tgz').extractall(extract)
accounts=[
  {{"UserName":"mf1","DisplayName":"متابع رئيسي 1","Password":"Mf@12345"}},
  {{"UserName":"mf2","DisplayName":"متابع رئيسي 2","Password":"Mf@12345"}},
]
for dest in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
    if not os.path.isfile(os.path.join(dest,'BE_SalesEmployee.dll')): continue
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f); rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel); os.makedirs(os.path.dirname(target), exist_ok=True); shutil.copy2(full, target)
    cfg=os.path.join(dest,'appsettings.json')
    if os.path.isfile(cfg):
        with open(cfg,'r',encoding='utf-8') as fh: data=json.load(fh)
        data['MainFollowerAccounts']=accounts
        with open(cfg,'w',encoding='utf-8') as fh: json.dump(data, fh, ensure_ascii=False, indent=2)
        print('PATCHED', cfg)
    print('UPDATED', dest)
subprocess.check_call(['systemctl','restart','qalaa-sales-gateway'])
subprocess.check_call(['systemctl','restart','qalaa-demo-sales-gateway'])
print('ACTIVE', subprocess.getoutput('systemctl is-active qalaa-sales-gateway qalaa-demo-sales-gateway'))
print('DONE')
"""
with c.open_sftp() as s2:
    with s2.file('/tmp/deploy_mf_phase1.py','w') as f: f.write(remote)
i,o,e=c.exec_command('python3 /tmp/deploy_mf_phase1.py', timeout=300)
print(o.read().decode('utf-8','replace'))
err=e.read().decode('utf-8','replace')
if err.strip(): print('STDERR', err[-1500:])
print('exit', o.channel.recv_exit_status())
c.close()
