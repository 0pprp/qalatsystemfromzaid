#!/usr/bin/env python3
"""Deploy director ratings: gateway + FE_Company."""
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
BE_GW = ROOT / "BE_SalesEmployee"
FE = ROOT / "FE_Company"
PUB_GW = ROOT / "_publish_gw_director"
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

    if PUB_GW.exists():
        shutil.rmtree(PUB_GW)
    print("dotnet publish gateway...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE_GW / "BE_SalesEmployee.csproj"), "-c", "Release", "-o", str(PUB_GW), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode

    print("npm run build FE...", flush=True)
    r2 = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r2.returncode:
        print(r2.stdout[-2000:], r2.stderr[-2000:])
        return r2.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    gw_tar = ROOT / f"_publish_gw_director_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_director_{stamp}.tgz"
    for tar_path, src, skip in ((gw_tar, PUB_GW, True), (fe_tar, FE_DIST, False)):
        if tar_path.exists():
            tar_path.unlink()
        with tarfile.open(tar_path, "w:gz") as tar:
            for p in src.rglob("*"):
                if p.is_file() and (not skip or p.name not in SKIP):
                    tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(gw_tar), f"/tmp/gw_director_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_director_{stamp}.tgz")
        sftp.close()
        remote = f"""
import os, shutil, tarfile, subprocess
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}

def extract(tgz, dest):
    shutil.rmtree(dest, ignore_errors=True)
    os.makedirs(dest, exist_ok=True)
    tarfile.open(tgz).extractall(dest)

def copy_tree(src, dest, skip_settings=False):
    for root,_,files in os.walk(src):
        for f in files:
            if skip_settings and f in skip: continue
            full=os.path.join(root,f)
            rel=os.path.relpath(full, src)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)

extract(f'/tmp/gw_director_{{stamp}}.tgz', f'/tmp/gw_director_{{stamp}}')
for d in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
    if os.path.isfile(os.path.join(d,'BE_SalesEmployee.dll')):
        copy_tree(f'/tmp/gw_director_{{stamp}}', d, True)
        print('GW', d)

extract(f'/tmp/fe_director_{{stamp}}.tgz', f'/tmp/fe_director_{{stamp}}')
for dest in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
    if not os.path.isdir(dest): continue
    for name in os.listdir(f'/tmp/fe_director_{{stamp}}'):
        src=os.path.join(f'/tmp/fe_director_{{stamp}}', name)
        dst=os.path.join(dest, name)
        if os.path.isdir(src):
            if os.path.isdir(dst): shutil.rmtree(dst)
            shutil.copytree(src, dst)
        else:
            shutil.copy2(src, dst)
    print('FE', dest)

for u in ['qalaa-sales-gateway','qalaa-demo-sales-gateway']:
    subprocess.call(['systemctl','restart',u])
subprocess.call(['systemctl','reload','nginx'])
print('DONE')
"""
        i, o, e = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY", timeout=300)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
