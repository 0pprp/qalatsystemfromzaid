#!/usr/bin/env python3
"""Deploy sales-request cascade soft-delete: BE_Company + gateway + FE_Company."""
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
BE_COMPANY = ROOT / "BE_Company"
BE_GW = ROOT / "BE_SalesEmployee"
FE = ROOT / "FE_Company"
PUB_BE = ROOT / "_publish_be_cascade_delete"
PUB_GW = ROOT / "_publish_gw_cascade_delete"
FE_DIST = FE / "dist"
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


def make_tar(src: Path, tar_path: Path, skip_settings: bool) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if not p.is_file():
                continue
            if skip_settings and p.name in SKIP:
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
    be_tar = ROOT / f"_publish_be_cascade_{stamp}.tgz"
    gw_tar = ROOT / f"_publish_gw_cascade_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_cascade_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(FE_DIST, fe_tar, False)
    print(
        f"BE {be_tar.stat().st_size/1024/1024:.1f}MB GW {gw_tar.stat().st_size/1024/1024:.1f}MB "
        f"FE {fe_tar.stat().st_size/1024/1024:.1f}MB",
        flush=True,
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_cascade_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_cascade_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_cascade_{stamp}.tgz")
        sftp.close()

        remote = f"""
import glob, os, shutil, tarfile, subprocess
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

be_extract=sync_extract(f'/tmp/be_cascade_{{stamp}}.tgz', f'/tmp/be_cascade_{{stamp}}')
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
    copy_tree(be_extract, dest)
    print('UPDATED', dest)

gw_extract=sync_extract(f'/tmp/gw_cascade_{{stamp}}.tgz', f'/tmp/gw_cascade_{{stamp}}')
gw_targets=[]
for d in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
    if os.path.isfile(os.path.join(d,'BE_SalesEmployee.dll')):
        gw_targets.append(d)
print('GW_TARGETS', len(gw_targets), gw_targets)
for dest in gw_targets:
    copy_tree(gw_extract, dest)
    print('GW_UPDATED', dest)

fe_extract=sync_extract(f'/tmp/fe_cascade_{{stamp}}.tgz', f'/tmp/fe_cascade_{{stamp}}')
for dest in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
    if not os.path.isdir(dest): continue
    for name in os.listdir(fe_extract):
        src=os.path.join(fe_extract, name)
        dst=os.path.join(dest, name)
        if os.path.isdir(src):
            if os.path.isdir(dst): shutil.rmtree(dst)
            shutil.copytree(src, dst)
        else:
            shutil.copy2(src, dst)
    print('FE_UPDATED', dest)

units=[]
for u in ['qalaa-sales-gateway','qalaa-demo-sales-gateway','qalaa-demo-backend']:
    if subprocess.call(['systemctl','is-active',u], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)==0:
        units.append(u)
for d in targets:
    cmd = (
        "for u in $(systemctl list-units --type=service --all --no-legend | awk '{{print $1}}'); do "
        f'wd=$(systemctl show "$u" -p WorkingDirectory --value 2>/dev/null || true); '
        f'if [ "$wd" = "{{d}}" ]; then echo "$u"; fi; '
        "done"
    )
    p=subprocess.run(['bash','-lc', cmd], capture_output=True, text=True)
    for line in (p.stdout or '').splitlines():
        u=line.strip()
        if u and u not in units:
            units.append(u)
print('RESTART', units)
for u in units:
    subprocess.call(['systemctl','restart',u])
subprocess.call(['systemctl','reload','nginx'])
print('DONE')
"""
        stdin, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY", timeout=600)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[-2000:])
        code = stdout.channel.recv_exit_status()
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
