#!/usr/bin/env python3
"""Deploy BE_Company (return + unread COUNT) + FE AdminFrontend assets."""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PROJ = ROOT / "BE_Company"
FE = ROOT / "FE_Company"
PUBLISH = ROOT / "_publish_be_return_perf"
FE_DIST = FE / "dist"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUBLISH.exists():
        shutil.rmtree(PUBLISH)
    PUBLISH.mkdir(parents=True)
    print("dotnet publish...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(PROJ / "BE_Company.csproj"), "-c", "Release", "-o", str(PUBLISH), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-3000:])
        print(r.stderr[-3000:])
        return r.returncode
    print("BE publish ok", flush=True)

    print("npm run build FE...", flush=True)
    r2 = subprocess.run(
        ["npm", "run", "build"],
        cwd=str(FE),
        capture_output=True,
        text=True,
        shell=True,
    )
    if r2.returncode != 0:
        print(r2.stdout[-3000:])
        print(r2.stderr[-3000:])
        return r2.returncode
    if not FE_DIST.is_dir():
        print("FE dist missing")
        return 2
    print("FE build ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_return_perf_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_return_perf_{stamp}.tgz"
    for p in (be_tar, fe_tar):
        if p.exists():
            p.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUBLISH.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUBLISH)).replace("\\", "/"))
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))
    print(f"BE tar {be_tar.stat().st_size/1024/1024:.1f}MB FE tar {fe_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_return_perf_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_return_perf_{stamp}.tgz")
        sftp.close()

        remote = f"""
import glob, os, shutil, tarfile, subprocess, time
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/be_return_perf_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/be_return_perf_{{stamp}}.tgz').extractall(extract)
targets=[]
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    if not os.path.isfile(os.path.join(d,'BE_Company.dll')): continue
    base=os.path.basename(d).lower()
    if 'backup' in base or base.endswith('-old') or 'frontend' in base: continue
    targets.append(d)
demo='/opt/qalaa-demo/backend'
if os.path.isfile(os.path.join(demo,'BE_Company.dll')):
    targets.append(demo)
print('BE_TARGETS', len(targets))
for dest in targets:
    bak=f'/opt/qalaa-backups/{{os.path.basename(dest)}}-pre-return-{{stamp}}'
    os.makedirs(bak, exist_ok=True)
    for f in ('BE_Company.dll','BE_Company.pdb'):
        src=os.path.join(dest,f)
        if os.path.isfile(src): shutil.copy2(src, os.path.join(bak,f))
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f)
            rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print('UPDATED', dest)

# restart admin/demo backends
units=subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-admin*' 'qalaa-demo-backend'").splitlines()
for line in units:
    u=line.split()[0] if line.strip() else ''
    if not u: continue
    name=u.removesuffix('.service')
    if 'backup' in name: continue
    subprocess.run(['systemctl','restart',name], check=False)
    print('RESTART', name, subprocess.getoutput(f'systemctl is-active {{name}}'))

# FE to AdminFrontend + demo frontend
fe_extract=f'/tmp/fe_return_perf_{{stamp}}'
shutil.rmtree(fe_extract, ignore_errors=True)
os.makedirs(fe_extract, exist_ok=True)
tarfile.open(f'/tmp/fe_return_perf_{{stamp}}.tgz').extractall(fe_extract)
for dest in ('/opt/qalaa-runtime/AdminFrontend','/opt/qalaa-demo/frontend'):
    if not os.path.isdir(dest):
        print('SKIP_FE', dest); continue
    bak=f'/opt/qalaa-backups/{{os.path.basename(dest)}}-pre-return-{{stamp}}'
    os.makedirs(bak, exist_ok=True)
    # backup index only for speed
    if os.path.isfile(os.path.join(dest,'index.html')):
        shutil.copy2(os.path.join(dest,'index.html'), os.path.join(bak,'index.html'))
    # sync assets: copy new files over
    for root,_,files in os.walk(fe_extract):
        for f in files:
            full=os.path.join(root,f)
            rel=os.path.relpath(full, fe_extract)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print('FE_UPDATED', dest)
subprocess.run(['nginx','-t'], check=False)
subprocess.run(['systemctl','reload','nginx'], check=False)
print('DONE')
"""
        with c.open_sftp() as s2:
            with s2.file("/tmp/deploy_return_perf.py", "w") as f:
                f.write(remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/deploy_return_perf.py", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
