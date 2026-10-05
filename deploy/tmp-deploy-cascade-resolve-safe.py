#!/usr/bin/env python3
"""Fast BE_Company-only redeploy after cascade resolve safety fix."""
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
PUB_BE = ROOT / "_publish_be_cascade_safe"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if PUB_BE.exists():
        shutil.rmtree(PUB_BE)
    PUB_BE.mkdir(parents=True)
    print("dotnet publish BE_Company...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE_COMPANY / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB_BE), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode
    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_cascade_safe_{stamp}.tgz"
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB_BE.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_BE)).replace("\\", "/"))
    print(f"BE {be_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        remote_tgz = f"/tmp/be_cascade_safe_{stamp}.tgz"
        sftp.put(str(be_tar), remote_tgz)
        sftp.close()
        remote = f"""
import glob, os, shutil, tarfile, subprocess
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
extract=f'/tmp/be_cascade_safe_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tarfile.open(f'/tmp/be_cascade_safe_{{stamp}}.tgz').extractall(extract)
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
    for root,_,files in os.walk(extract):
        for f in files:
            if f in skip: continue
            full=os.path.join(root,f)
            rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print('UPDATED', os.path.basename(dest))
units=[]
for d in targets:
    cmd=(
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
print('RESTART', len(units))
for u in units:
    subprocess.call(['systemctl','restart',u])
print('DONE')
"""
        _, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
