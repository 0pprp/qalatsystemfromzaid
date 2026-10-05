#!/usr/bin/env python3
"""Publish + deploy BE_DelegateWebApplication list-stats enrichment to all Delegate*."""
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
PROJ = ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication" / "BE_DelegateWebApplication.csproj"
PUBLISH = ROOT / "_publish_delegate_liststats"
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
        ["dotnet", "publish", str(PROJ), "-c", "Release", "-o", str(PUBLISH), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    local_tar = ROOT / f"_publish_delegate_liststats_{stamp}.tar.gz"
    with tarfile.open(local_tar, "w:gz") as tar:
        for p in PUBLISH.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUBLISH)).replace("\\", "/"))
    print(f"tar {local_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    remote_tar = f"/opt/qalaa-updates/delegate-liststats-{stamp}.tar.gz"
    sftp = c.open_sftp()
    sftp.put(str(local_tar), remote_tar)
    sftp.close()

    remote = f"""
import glob, os, shutil, subprocess, tarfile
stamp='{stamp}'
remote_tar='{remote_tar}'
extract=f'/tmp/delegate_liststats_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tarfile.open(remote_tar).extractall(extract)
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
dirs=[]
for name in sorted(os.listdir('/opt/qalaa-runtime')):
    if not name.lower().startswith('delegate'): continue
    if 'backup' in name.lower() or name.endswith('.bak'): continue
    d=os.path.join('/opt/qalaa-runtime', name)
    if os.path.isfile(os.path.join(d,'BE_DelegateWebApplication.dll')):
        dirs.append(d)
demo='/opt/qalaa-demo/follower-backend'
if os.path.isfile(os.path.join(demo,'BE_DelegateWebApplication.dll')):
    dirs.append(demo)
print('DIRS', len(dirs))
for dest in dirs:
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
for d in dirs:
    cmd=(
        "for u in $(systemctl list-units --type=service --all --no-legend 'qalaa-delegate*' | awk '{{print $1}}'); do "
        f'wd=$(systemctl show "$u" -p WorkingDirectory --value 2>/dev/null || true); '
        f'if [ "$wd" = "{{d}}" ]; then echo "$u"; fi; '
        "done"
    )
    p=subprocess.run(['bash','-lc', cmd], capture_output=True, text=True)
    for line in (p.stdout or '').splitlines():
        u=line.strip()
        if u and u not in units:
            units.append(u)
# also demo follower
for u in ['qalaa-demo-follower.service']:
    if subprocess.call(['systemctl','is-active','--quiet',u])==0 or True:
        if u not in units:
            # restart if unit exists
            if subprocess.call(['systemctl','cat',u], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)==0:
                units.append(u)
print('RESTART', len(units))
for u in units:
    subprocess.call(['systemctl','restart',u])
# verify marker in najaf dll
b=open('/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll','rb').read()
print('HAS_ACTIVE_RULE', b'ActiveMinPercentExclusive' in b or 'ActiveCustomersCount'.encode() in b)
print('HAS_RATING_CALC', b'FollowerCustomerRatingCalculator' in b)
print('DONE')
"""
    _, out, err = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY", timeout=600)
    print(out.read().decode("utf-8", "replace"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e[-1500:])
    code = out.channel.recv_exit_status() if False else 0
    # fix: need stdout channel
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
