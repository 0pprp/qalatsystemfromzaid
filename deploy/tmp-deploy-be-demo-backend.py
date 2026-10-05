#!/usr/bin/env python3
"""Deploy BE_Company publish output ONLY to demo backend (:8080)."""
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
REMOTE_DIR = "/opt/qalaa-demo/backend"
UNIT = "qalaa-demo-backend"
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
        [
            "dotnet",
            "publish",
            str(PROJ / "BE_Company.csproj"),
            "-c",
            "Release",
            "-o",
            str(PUBLISH),
            "--nologo",
        ],
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
        script = f"""
set -e
test -d {REMOTE_DIR}
python3 - <<'PY'
import os, shutil, tarfile, subprocess
remote_dir = {REMOTE_DIR!r}
extract = '/tmp/be_bm_extract'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
with tarfile.open('/tmp/be_bm_publish.tgz') as tar:
    tar.extractall(extract)
skip = {{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
for root, dirs, files in os.walk(extract):
    for f in files:
        if f in skip:
            continue
        src = os.path.join(root, f)
        rel = os.path.relpath(src, extract)
        dst = os.path.join(remote_dir, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(src, dst)
print('UPDATED', remote_dir)
subprocess.run(['systemctl', 'restart', {UNIT!r}], check=True)
print('RESTARTED', {UNIT!r})
print('WD', subprocess.check_output(['systemctl','show',{UNIT!r},'-p','WorkingDirectory','--value'], text=True).strip())
print('ACTIVE', subprocess.check_output(['systemctl','is-active',{UNIT!r}], text=True).strip())
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
