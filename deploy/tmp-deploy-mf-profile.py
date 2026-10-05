#!/usr/bin/env python3
"""Deploy BE_SalesEmployee (MF customer profile) to demo+prod gateways and smoke profile route."""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PROJ = ROOT / "BE_SalesEmployee"
PUB = ROOT / "_publish_gw_mf_profile"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if PUB.exists():
        shutil.rmtree(PUB)
    PUB.mkdir(parents=True)
    print("publish...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(PROJ / "BE_SalesEmployee.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:])
        print(r.stderr[-2000:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_publish_gw_mf_profile_{stamp}.tgz"
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    print("tar", tar_path.stat().st_size // 1024, "KB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/gw_mf_profile_{stamp}.tgz")
        sftp.close()
        remote = f"""
import json, os, shutil, tarfile, subprocess, urllib.request, urllib.error, time
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/gw_mf_profile_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True); os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/gw_mf_profile_{{stamp}}.tgz').extractall(extract)
for dest in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
    if not os.path.isfile(os.path.join(dest,'BE_SalesEmployee.dll')): continue
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f); rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel); os.makedirs(os.path.dirname(target), exist_ok=True); shutil.copy2(full, target)
    print('UPDATED', dest)
subprocess.check_call(['systemctl','restart','qalaa-demo-sales-gateway','qalaa-sales-gateway'])
time.sleep(5)
base='http://127.0.0.1:5403/api'
env=open('/etc/qalaa-demo/sales-gateway.env',encoding='utf-8',errors='replace').read()
pw=None
for line in env.splitlines():
  if line.startswith('MainFollowerAccounts__0__Password='):
    pw=line.split('=',1)[1]

def post(path, body, token=None):
    headers={{'Content-Type':'application/json'}}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, data=json.dumps(body).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())

def get(path, token=None):
    headers={{}}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, headers=headers)
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())

mf=post('/Auth/LoginMainFollower', {{'userName':'mf1','password':pw}})
missions=get('/main-follower/missions', mf['token'])
print('MISSIONS', len(missions))
if not missions:
    print('NO_MISSIONS'); raise SystemExit(1)
m=missions[0]
city=m['cityValues'][0]
# sales list then profile
sales=get(f"/main-follower/missions/{{m['id']}}/cities/{{city}}/sales", mf['token'])
print('SALES', len(sales) if isinstance(sales, list) else type(sales))
cid=None
name=None
phone=None
if isinstance(sales, list) and sales:
    row=sales[0]
    cid=row.get('customerId') or row.get('CustomerId')
    name=row.get('customerName') or row.get('CustomerName')
    phone=row.get('customerPhone') or row.get('CustomerPhone')
    print('SAMPLE', cid, name, phone)
try:
    if cid:
        profile=get(f"/main-follower/missions/{{m['id']}}/cities/{{city}}/customers/{{int(cid)}}/profile", mf['token'])
    else:
        q=[]
        if name: q.append('name='+urllib.parse.quote(str(name)))
        if phone: q.append('phone='+urllib.parse.quote(str(phone)))
        import urllib.parse
        profile=get(f"/main-follower/missions/{{m['id']}}/cities/{{city}}/customers/profile?"+'&'.join(q), mf['token'])
    print('PROFILE', profile.get('customerName') or profile.get('CustomerName'),
          'docs', len(profile.get('customerDocuments') or profile.get('CustomerDocuments') or []),
          'notes', len(profile.get('notes') or profile.get('Notes') or []),
          'evals', len(profile.get('evaluations') or profile.get('Evaluations') or []))
except Exception as e:
    print('PROFILE_FAIL', e)
print('DONE')
"""
        # fix urllib.parse import order in remote script
        remote = remote.replace(
            "try:\n    if cid:\n",
            "import urllib.parse\ntry:\n    if cid:\n",
        ).replace(
            "        import urllib.parse\n        profile=get",
            "        profile=get",
        )
        with c.open_sftp() as s2:
            with s2.file("/tmp/deploy_mf_profile.py", "w") as f:
                f.write(remote)
        i, o, e = c.exec_command("python3 /tmp/deploy_mf_profile.py", timeout=300)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
