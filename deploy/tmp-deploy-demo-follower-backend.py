#!/usr/bin/env python3
"""Deploy BE_Delegate Release build to DEMO follower backend only (8081/5402)."""
from __future__ import annotations

import os
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUBLISH = ROOT / "_publish_demo_follower"
DEST = "/opt/qalaa-demo/follower-backend"
SERVICE = "qalaa-demo-follower"
SKIP_FILES = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not PUBLISH.is_dir():
        print("missing publish", PUBLISH)
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    local_tar = ROOT / f"_publish_demo_follower_{stamp}.tar.gz"
    remote_tar = f"/opt/qalaa-updates/demo-follower-{stamp}.tar.gz"
    if local_tar.exists():
        local_tar.unlink()
    with tarfile.open(local_tar, "w:gz") as tar:
        for root, _dirs, files in os.walk(PUBLISH):
            for name in files:
                if name.lower() in {x.lower() for x in SKIP_FILES}:
                    continue
                full = Path(root) / name
                tar.add(full, arcname=full.relative_to(PUBLISH).as_posix())
    print(f"Tar MB={local_tar.stat().st_size / (1024 * 1024):.1f}", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        c.exec_command("mkdir -p /opt/qalaa-updates")[1].channel.recv_exit_status()
        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        sftp.close()

        inner = f"""
import os, shutil, subprocess, tarfile, time
dest='{DEST}'
bak=dest + '-bak-{stamp}'
os.makedirs(bak, exist_ok=True)
# backup key dll
for name in ('BE_DelegateWebApplication.dll','BE_DelegateWebApplication.pdb'):
    src=os.path.join(dest, name)
    if os.path.isfile(src):
        shutil.copy2(src, os.path.join(bak, name))
subprocess.run(['systemctl','stop','{SERVICE}'], check=False)
time.sleep(1)
extract='/tmp/demo-follower-extract-{stamp}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
with tarfile.open('{remote_tar}', 'r:gz') as t:
    t.extractall(extract)
for root, dirs, files in os.walk(extract):
    for f in files:
        full=os.path.join(root, f)
        rel=os.path.relpath(full, extract)
        target=os.path.join(dest, rel)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        shutil.copy2(full, target)
subprocess.run(['chown','-R','qalaa:qalaa', dest], check=False)
subprocess.run(['systemctl','start','{SERVICE}'], check=False)
time.sleep(2)
print('ACTIVE', subprocess.getoutput('systemctl is-active {SERVICE}'))
print('DLL', os.path.getmtime(os.path.join(dest,'BE_DelegateWebApplication.dll')))
"""
        with c.open_sftp() as sftp2:
            with sftp2.file("/tmp/deploy_demo_follower.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/deploy_demo_follower.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        code = stdout.channel.recv_exit_status()
        if code != 0:
            return code

        # smoke
        smoke = r"""
import json, urllib.request, urllib.parse, urllib.error, time
time.sleep(1)
req=urllib.request.Request('http://127.0.0.1:5402/api/Followers/Login', data=json.dumps({'userName':'ahmed4','password':'ahmed4'}).encode(), headers={'Content-Type':'application/json'}, method='POST')
login=json.loads(urllib.request.urlopen(req, timeout=20).read().decode())
a=login['asyncId']
print('LOGIN_OK', a)
for path in ['Lists','ListStats','FieldFollowups','PendingSales','LegalFollowups']:
    try:
        with urllib.request.urlopen(f'http://127.0.0.1:8081/api/Followers/{path}?asyncId={a}', timeout=45) as resp:
            print(path, resp.status, len(resp.read()))
    except urllib.error.HTTPError as e:
        print(path, 'HTTP', e.code, e.read()[:160])
print('DONE')
"""
        with c.open_sftp() as sftp3:
            with sftp3.file("/tmp/smoke_demo_follower.py", "w") as f:
                f.write(smoke)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/smoke_demo_follower.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("SMOKE_ERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
