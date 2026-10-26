#!/usr/bin/env python3
"""Deploy MainFollower phase 3: staff reviews + company delegates list."""
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
BE_COMPANY = ROOT / "BE_Company"
BE_GW = ROOT / "BE_SalesEmployee"
PUB_BE = ROOT / "_publish_be_mf_p3"
PUB_GW = ROOT / "_publish_gw_mf_p3"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def publish(proj: Path, out: Path, label: str) -> int:
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    print(f"dotnet publish {label}...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(proj), "-c", "Release", "-o", str(out), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-3000:])
        print(r.stderr[-3000:])
        return r.returncode
    print(f"{label} publish ok", flush=True)
    return 0


def make_tar(src: Path, tar_path: Path) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if not p.is_file():
                continue
            if p.name in SKIP:
                continue
            tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if publish(BE_COMPANY / "BE_Company.csproj", PUB_BE, "BE_Company"):
        return 1
    if publish(BE_GW / "BE_SalesEmployee.csproj", PUB_GW, "BE_SalesEmployee"):
        return 1

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_mf_p3_{stamp}.tgz"
    gw_tar = ROOT / f"_publish_gw_mf_p3_{stamp}.tgz"
    make_tar(PUB_BE, be_tar)
    make_tar(PUB_GW, gw_tar)
    print(
        f"BE {be_tar.stat().st_size/1024/1024:.1f}MB GW {gw_tar.stat().st_size/1024/1024:.1f}MB",
        flush=True,
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_mf_p3_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_mf_p3_{stamp}.tgz")
        sftp.close()

        remote = f"""
import glob, json, os, shutil, tarfile, subprocess, urllib.request, urllib.error, time
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}

def sync_extract(tgz, extract):
    shutil.rmtree(extract, ignore_errors=True)
    os.makedirs(extract, exist_ok=True)
    tarfile.open(tgz).extractall(extract)
    return extract

def copy_tree(extract, dest):
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f)
            rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)

be_extract=sync_extract(f'/tmp/be_mf_p3_{{stamp}}.tgz', f'/tmp/be_mf_p3_{{stamp}}')
targets=[]
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    if not os.path.isfile(os.path.join(d,'BE_Company.dll')): continue
    base=os.path.basename(d).lower()
    # Never touch .bak-* snapshots — copying live publish into them while services run
    # previously corrupted AdminNajaf and returned 500 on province login.
    if '.bak' in base or 'backup' in base or base.endswith('-old') or 'frontend' in base: continue
    targets.append(d)
demo='/opt/qalaa-demo/backend'
if os.path.isfile(os.path.join(demo,'BE_Company.dll')):
    targets.append(demo)
print('BE_TARGETS', len(targets))
for dest in targets:
    copy_tree(be_extract, dest)
    print('UPDATED', dest)

gw_extract=sync_extract(f'/tmp/gw_mf_p3_{{stamp}}.tgz', f'/tmp/gw_mf_p3_{{stamp}}')
for dest in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
    if not os.path.isfile(os.path.join(dest,'BE_SalesEmployee.dll')): continue
    copy_tree(gw_extract, dest)
    print('GW_UPDATED', dest)

units=['qalaa-sales-gateway','qalaa-demo-sales-gateway']
# restart demo admin + common admin units used by gateway fan-out
for u in list(units):
    pass
# discover admin units
for d in targets:
    base=os.path.basename(d)
    for cand in [f'qalaa-{{base}}', f'qalaa-company-{{base.lower()}}', f'qalaa-admin-{{base.lower()}}']:
        if subprocess.call(['systemctl','cat',cand], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)==0:
            if cand not in units: units.append(cand)
# also restart demo backend if present
for cand in ['qalaa-demo-backend','qalaa-demo-admin']:
    if subprocess.call(['systemctl','cat',cand], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)==0:
        if cand not in units: units.append(cand)

print('RESTART', units)
for u in units:
    subprocess.call(['systemctl','restart',u])
time.sleep(4)

base='http://127.0.0.1:5403/api'

def post(path, body, token=None):
    headers={{'Content-Type':'application/json'}}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, data=json.dumps(body).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())

def get(path, token):
    req=urllib.request.Request(base+path, headers={{'Authorization':'Bearer '+token}})
    with urllib.request.urlopen(req, timeout=90) as r:
        return json.loads(r.read().decode())

mf=post('/Auth/LoginMainFollower', {{'userName':'mf1','password':os.environ.get('MF_PASSWORD','')}})
dm=post('/Auth/LoginDelegatedManager', {{'userName':'dm','password':'dm'}})
committee=post('/delegated-manager/committees', {{'name':'لجنة مرحلة3','memberUserNames':['mf1','mf2']}}, dm['token'])
mission=post('/delegated-manager/committees/missions', {{
  'committeeId':committee['id'],
  'cityValues':['DatabaseCompanyNajaf_DEMO','1'],
  'note':'p3 staff'
}}, dm['token'])
print('MISSION', mission['id'], mission['cityValues'])
city=mission['cityValues'][0]
token=mf['token']

# probe staff lists
for leaf in ['delegates','followers','sales-employees']:
    try:
        rows=get(f"/main-follower/missions/{{mission['id']}}/cities/{{city}}/{{leaf}}", token)
        print(leaf.upper(), type(rows).__name__, len(rows) if isinstance(rows,list) else rows)
        if isinstance(rows,list) and rows and leaf=='delegates':
            row=rows[0]
            tid=row.get('delegateId') or row.get('DelegateID') or 55
            tname=row.get('delegateName') or row.get('DelegateName') or 'مندوب'
            city_used=city
    except Exception as ex:
        print(leaf.upper()+'_FAIL', ex)
        tid=55; tname='مندوب اختبار'; city_used=city

# submit staff review
rev=post('/main-follower/staff-reviews', {{
  'missionId': mission['id'], 'cityValue': city_used,
  'targetType': 'Delegate', 'targetId': int(tid), 'targetName': tname,
  'listId': int(tid), 'score': 8, 'note': 'أداء جيد في التحصيل'
}}, token)
print('STAFF_REVIEW', rev.get('id'), rev.get('score'))
print('FEED', (rev.get('feedNote') or '')[:80])

# reject high score without note
try:
    post('/main-follower/staff-reviews', {{
      'missionId': mission['id'], 'cityValue': city_used,
      'targetType': 'SalesEmployee', 'targetId': 9, 'targetName': 'x',
      'score': 9, 'note': ''
    }}, token)
    print('UNEXPECTED_OK')
except urllib.error.HTTPError as e:
    print('REJECT_OK', e.code, e.read().decode()[:120])

feed=get(f"/main-follower/staff/Delegate/{{int(tid)}}/notes?cityValue={{city_used}}", token)
print('TARGET_FEED', len(feed))
mission_rows=get(f"/main-follower/missions/{{mission['id']}}/staff-reviews", token)
print('MISSION_STAFF', len(mission_rows))
print('DONE')
"""
        with c.open_sftp() as s2:
            with s2.file("/tmp/deploy_mf_p3.py", "w") as f:
                f.write(remote)
        i, o, e = c.exec_command("python3 /tmp/deploy_mf_p3.py", timeout=420)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-2000:])
        code = o.channel.recv_exit_status()
        print("exit", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
