#!/usr/bin/env python3
"""Repair BE_Company on all live Admin* runtimes after bad phase3 sync; restart units; smoke Najaf login."""
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
BE = ROOT / "BE_Company"
PUB = ROOT / "_publish_be_repair_najaf"
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
        ["dotnet", "publish", str(BE / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:])
        print(r.stderr[-2000:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_publish_be_repair_{stamp}.tgz"
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    print("tar", round(tar_path.stat().st_size / 1024 / 1024, 1), "MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/be_repair_{stamp}.tgz")
        sftp.close()

        remote = f"""
import glob, json, os, shutil, subprocess, tarfile, time, urllib.request, urllib.error
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/be_repair_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/be_repair_{{stamp}}.tgz').extractall(extract)

def copy_tree(src, dest):
    for root,_,files in os.walk(src):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f)
            rel=os.path.relpath(full, src)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)

targets=[]
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    base=os.path.basename(d)
    if '.bak' in base.lower() or base.lower().endswith('-old') or 'frontend' in base.lower():
        continue
    if not os.path.isfile(os.path.join(d,'BE_Company.dll')):
        continue
    targets.append(d)
demo='/opt/qalaa-demo/backend'
if os.path.isfile(os.path.join(demo,'BE_Company.dll')):
    targets.append(demo)
print('TARGETS', len(targets))
for dest in targets:
    copy_tree(extract, dest)
    print('UPDATED', dest)

# restart all live company units (not bak)
units=[]
out=subprocess.check_output(['systemctl','list-units','--type=service','--all','--no-pager','--plain'], text=True)
for line in out.splitlines():
    name=line.split()[0] if line.strip() else ''
    if not name.endswith('.service'):
        continue
    unit=name[:-8] if False else name  # keep .service
    # prefer qalaa-company-*
    if name.startswith('qalaa-company-') or name in ('qalaa-demo-backend.service',):
        units.append(name)
# unique preserve order
seen=set(); ordered=[]
for u in units:
    if u in seen: continue
    seen.add(u); ordered.append(u)
print('RESTART_COUNT', len(ordered))
for u in ordered:
    subprocess.call(['systemctl','restart',u])
time.sleep(6)
# active summary
bad=[]
for u in ordered:
    st=subprocess.check_output(['systemctl','is-active',u], text=True).strip()
    if st!='active':
        bad.append((u,st))
print('INACTIVE', bad[:20], 'count', len(bad))

# smoke Najaf login endpoint (expect non-500; wrong password ok)
url='http://sharenewnajaf.alsaaeidy.com/api/Users/Users_LoginAdmin'
req=urllib.request.Request(url, data=json.dumps({{'userName':'ahmed','password':'x'}}).encode(), headers={{'Content-Type':'application/json'}})
try:
    with urllib.request.urlopen(req, timeout=30) as r:
        body=r.read().decode('utf-8','replace')
        print('NAJAF_LOGIN', r.status, body[:200])
except urllib.error.HTTPError as e:
    body=e.read().decode('utf-8','replace')
    print('NAJAF_LOGIN_HTTP', e.code, body[:300])
except Exception as e:
    print('NAJAF_LOGIN_ERR', e)

# also demo
try:
    req=urllib.request.Request('http://127.0.0.1:5401/api/Users/Users_LoginAdmin', data=json.dumps({{'userName':'x','password':'y'}}).encode(), headers={{'Content-Type':'application/json'}})
    with urllib.request.urlopen(req, timeout=20) as r:
        print('DEMO_LOGIN', r.status, r.read()[:120])
except urllib.error.HTTPError as e:
    print('DEMO_LOGIN_HTTP', e.code, e.read()[:120])
except Exception as e:
    print('DEMO_LOGIN_ERR', e)
print('DONE')
"""
        with c.open_sftp() as s2:
            with s2.file("/tmp/repair_be_company.py", "w") as f:
                f.write(remote)
        i, o, e = c.exec_command("python3 /tmp/repair_be_company.py", timeout=420)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
