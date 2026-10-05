#!/usr/bin/env python3
"""Deploy slim sales-request list BE_Company to all Admin* + demo backend."""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tarfile
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PROJ = ROOT / "BE_Company"
PUBLISH = ROOT / "_publish_be_list_fast"
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
        print(r.stdout[-2500:])
        print(r.stderr[-2500:])
        return r.returncode
    print("publish ok", flush=True)

    tar_path = ROOT / "_publish_be_list_fast.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in PUBLISH.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUBLISH)).replace("\\", "/"))

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), "/tmp/be_list_fast.tgz")
        sftp.close()
        script = r"""
set -e
python3 <<'PY'
import glob, os, shutil, tarfile, subprocess
extract='/tmp/be_list_fast_extract'
skip={'appsettings.json','appsettings.Development.json','appsettings.Production.json'}
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tarfile.open('/tmp/be_list_fast.tgz').extractall(extract)

targets=[]
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    if not os.path.isfile(os.path.join(d,'BE_Company.dll')): continue
    base=os.path.basename(d).lower()
    if 'backup' in base or base.endswith('-old') or 'frontend' in base: continue
    targets.append(d)
demo='/opt/qalaa-demo/backend'
if os.path.isfile(os.path.join(demo,'BE_Company.dll')):
    targets.append(demo)

print('TARGETS', len(targets))
for d in targets:
    for root, dirs, files in os.walk(extract):
        for f in files:
            if f in skip:
                continue
            src=os.path.join(root,f)
            rel=os.path.relpath(src, extract)
            dst=os.path.join(d, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)
    print('UPDATED', os.path.basename(d))

for d in targets:
    cmd = (
        "for u in $(systemctl list-units --type=service --all --no-legend | awk '{print $1}'); do "
        f'wd=$(systemctl show "$u" -p WorkingDirectory --value 2>/dev/null || true); '
        f'if [ "$wd" = "{d}" ]; then systemctl restart "$u" && echo RESTART "$u"; fi; '
        "done"
    )
    subprocess.run(['bash','-lc', cmd], check=False)

subprocess.run(['bash','-lc','systemctl restart qalaa-demo-backend 2>/dev/null && echo RESTART qalaa-demo-backend || true'], check=False)
print('DONE')
PY
"""
        stdin, stdout, stderr = c.exec_command(script, timeout=600)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out)
        if err.strip():
            print("STDERR", err[:2000])
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
