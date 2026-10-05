#!/usr/bin/env python3
"""Publish BE_Company with BranchManager APIs to demo Admin on :8080."""
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
PUBLISH = ROOT / "_publish_be_bm"
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

    tar_path = ROOT / "_publish_be_bm.tgz"
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
        remote = "/tmp/be_bm_publish.tgz"
        sftp = c.open_sftp()
        sftp.put(str(tar_path), remote)
        sftp.close()
        # Find which Admin* serves :8080 / Najaf DEMO
        script = r"""
set -e
python3 - <<'PY'
import glob, os, subprocess, tarfile, shutil
# Prefer demo admin for Najaf
candidates=[]
for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    if not os.path.isfile(os.path.join(d,'BE_Company.dll')): continue
    base=os.path.basename(d)
    if 'backup' in base.lower() or base.endswith('-old'): continue
    candidates.append(d)
print('CANDIDATES', len(candidates))
# Deploy to all Admin* that look like demo OR all company admins (same binary)
# Safer: deploy only folders that contain Najaf_DEMO in appsettings
targets=[]
for d in candidates:
    cfg=os.path.join(d,'appsettings.json')
    text=''
    try:
        text=open(cfg,encoding='utf-8',errors='replace').read()
    except Exception:
        pass
    if 'DatabaseCompanyNajaf_DEMO' in text or '8080' in text or 'RequireDemoDatabase' in text:
        targets.append(d)
if not targets:
    # fallback: AdminNajaf*DEMO*
    targets=[d for d in candidates if 'demo' in d.lower() or 'Demo' in d]
if not targets:
    targets=candidates[:1]
print('TARGETS', targets)
tar=tarfile.open('/tmp/be_bm_publish.tgz')
extract='/tmp/be_bm_extract'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tar.extractall(extract)
tar.close()
for d in targets:
    for root, dirs, files in os.walk(extract):
        for f in files:
            if f in ('appsettings.json','appsettings.Development.json','appsettings.Production.json'):
                continue
            src=os.path.join(root,f)
            rel=os.path.relpath(src, extract)
            dst=os.path.join(d, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)
    print('UPDATED', d)
# restart services that bind demo
subprocess.run(['bash','-lc','systemctl list-units --type=service --all | grep -iE \"admin|qalaa|company|demo|8080\" | head -40'], check=False)
# try known unit names
for unit in ['qalaa-admin-najaf-demo','qalaa-demo-admin','admin-najaf-demo','qalaa-admincompany-demo']:
    subprocess.run(['bash','-lc', f'systemctl restart {unit} 2>/dev/null && echo RESTARTED {unit} || true'], check=False)
# broader: restart any systemd unit whose WorkingDirectory is a target
import configparser
for d in targets:
    base=os.path.basename(d)
    subprocess.run(['bash','-lc', f'systemctl list-units --all --no-legend | awk \"{{print \\$1}}\" | while read u; do systemctl show \"$u\" -p WorkingDirectory --value 2>/dev/null | grep -qx \"{d}\" && systemctl restart \"$u\" && echo RESTART_WD \"$u\"; done'], check=False)
print('DONE_TARGETS', len(targets))
PY
"""
        stdin, stdout, stderr = c.exec_command(script, timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out)
        if err.strip():
            print("STDERR", err[:800])
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
