#!/usr/bin/env python3
"""Deploy gateway phase4 + seed main-follower mobile update row + smoke history."""
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
PUB = ROOT / "_publish_gw_mf_p4"
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
    tar_path = ROOT / f"_publish_gw_mf_p4_{stamp}.tgz"
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
        sftp.put(str(tar_path), f"/tmp/gw_mf_p4_{stamp}.tgz")
        sftp.close()
        remote = f"""
import json, os, shutil, tarfile, subprocess, urllib.request, urllib.error, time
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/gw_mf_p4_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True); os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/gw_mf_p4_{{stamp}}.tgz').extractall(extract)
for dest in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
    if not os.path.isfile(os.path.join(dest,'BE_SalesEmployee.dll')): continue
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f); rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel); os.makedirs(os.path.dirname(target), exist_ok=True); shutil.copy2(full, target)
    print('UPDATED', dest)
subprocess.check_call(['systemctl','restart','qalaa-demo-sales-gateway','qalaa-sales-gateway'])
time.sleep(4)
base='http://127.0.0.1:5403/api'

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

# mobile update check for main-follower (in-memory seed after restart)
try:
    upd=get('/mobile-updates/main-follower?currentVersionCode=1')
    print('UPDATE', upd.get('updateKind'), upd.get('latestVersionCode'), upd.get('mandatory'))
except Exception as e:
    print('UPDATE_FAIL', e)

mf=post('/Auth/LoginMainFollower', {{'userName':'mf1','password':'Mf@12345'}})
dm=post('/Auth/LoginDelegatedManager', {{'userName':'dm','password':'dm'}})
committee=post('/delegated-manager/committees', {{'name':'لجنة مرحلة4','memberUserNames':['mf1','mf2']}}, dm['token'])
mission=post('/delegated-manager/committees/missions', {{
  'committeeId':committee['id'],
  'cityValues':['DatabaseCompanyNajaf_DEMO'],
  'note':'p4 history'
}}, dm['token'])
city=mission['cityValues'][0]
# create reviews
post('/main-follower/sale-reviews', {{
  'missionId': mission['id'], 'cityValue': city, 'saleId': 501,
  'customerName': 'زبون سجل', 'score': 8, 'note': 'سجل دائم'
}}, mf['token'])
post('/main-follower/staff-reviews', {{
  'missionId': mission['id'], 'cityValue': city, 'targetType': 'Delegate',
  'targetId': 12, 'targetName': 'مندوب', 'listId': 12, 'score': 6, 'note': None
}}, mf['token'])
hist=get(f"/main-follower/cities/{{city}}/history", mf['token'])
print('HISTORY', len(hist), [h.get('kind') for h in hist])
summary=get(f"/delegated-manager/committees/missions/{{mission['id']}}/summary", dm['token'])
print('SUMMARY', summary.get('saleReviewCount'), summary.get('staffReviewCount'), summary.get('avgSaleScore'))
print('DONE')
"""
        with c.open_sftp() as s2:
            with s2.file("/tmp/deploy_mf_p4.py", "w") as f:
                f.write(remote)
        i, o, e = c.exec_command("python3 /tmp/deploy_mf_p4.py", timeout=300)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
