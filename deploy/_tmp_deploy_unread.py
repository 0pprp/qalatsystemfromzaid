from __future__ import annotations
import os, shutil, subprocess, tarfile, time
from pathlib import Path
import paramiko
HOST="169.58.236.52"
ROOT=Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")
PROJ=ROOT/"BE_Company"
PUB=ROOT/"_publish_be_unread_fix"
SKIP={"appsettings.json","appsettings.Development.json","appsettings.Production.json"}
if PUB.exists(): shutil.rmtree(PUB)
PUB.mkdir(parents=True)
print("publish...", flush=True)
r=subprocess.run(["dotnet","publish",str(PROJ/"BE_Company.csproj"),"-c","Release","-o",str(PUB),"--nologo"],capture_output=True,text=True)
if r.returncode: print(r.stdout[-2000:]); print(r.stderr[-2000:]); raise SystemExit(r.returncode)
stamp=time.strftime("%Y%m%d_%H%M%S")
tar_path=ROOT/f"_publish_be_unread_{stamp}.tgz"
with tarfile.open(tar_path,"w:gz") as tar:
    for p in PUB.rglob("*"):
        if p.is_file() and p.name not in SKIP:
            tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\","/"))
print("tar", tar_path.stat().st_size//1024, "KB", flush=True)
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
sftp=c.open_sftp(); sftp.put(str(tar_path),f"/tmp/be_unread_{stamp}.tgz"); sftp.close()
remote=f"""
import glob,os,shutil,tarfile,subprocess
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/be_unread_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True); os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/be_unread_{{stamp}}.tgz').extractall(extract)
targets=[]
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    if not os.path.isfile(os.path.join(d,'BE_Company.dll')): continue
    base=os.path.basename(d).lower()
    if 'backup' in base or base.endswith('-old') or 'frontend' in base: continue
    targets.append(d)
demo='/opt/qalaa-demo/backend'
if os.path.isfile(os.path.join(demo,'BE_Company.dll')): targets.append(demo)
print('TARGETS', len(targets))
for dest in targets:
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f); rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel); os.makedirs(os.path.dirname(target), exist_ok=True); shutil.copy2(full, target)
    print('UPDATED', dest)
out=subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-company-*' 'qalaa-demo-backend'")
n=0
for line in out.splitlines():
    parts=line.split()
    if not parts: continue
    name=parts[0].removesuffix('.service')
    if 'backup' in name: continue
    subprocess.run(['systemctl','restart',name], check=False); n+=1
print('RESTARTED', n)
print('DONE')
"""
with c.open_sftp() as s2:
    with s2.file("/tmp/deploy_unread_fix.py","w") as f: f.write(remote)
stdin,stdout,stderr=c.exec_command("python3 /tmp/deploy_unread_fix.py", timeout=600)
print(stdout.read().decode("utf-8","replace"))
err=stderr.read().decode("utf-8","replace")
if err.strip(): print("STDERR", err[-1500:])
print("exit", stdout.channel.recv_exit_status())
c.close()
