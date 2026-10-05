#!/usr/bin/env python3
"""Deploy BE_SalesEmployee only (WrongSale tab dashboard counters). No Flutter OTA."""
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
PROJ = ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj"
PUBLISH = ROOT / "_publish_wrongsale_tab_gw"
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
    local_tar = ROOT / f"_publish_wrongsale_tab_gw_{stamp}.tar.gz"
    with tarfile.open(local_tar, "w:gz") as tar:
        for p in PUBLISH.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUBLISH)).replace("\\", "/"))
    print(f"tar {local_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    remote_tar = f"/opt/qalaa-updates/wrongsale-tab-gw-{stamp}.tar.gz"
    sftp = c.open_sftp()
    sftp.put(str(local_tar), remote_tar)
    sftp.close()

    remote = f"""
import os, shutil, subprocess, tarfile
stamp='{stamp}'
remote_tar='{remote_tar}'
extract=f'/tmp/wrongsale_tab_gw_{{stamp}}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
tarfile.open(remote_tar).extractall(extract)
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}
dirs=[d for d in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway','/opt/qalaa-runtime/BE_SalesEmployee'] if os.path.isfile(os.path.join(d,'BE_SalesEmployee.dll'))]
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
for u in ('qalaa-sales-gateway','qalaa-demo-sales-gateway'):
    if subprocess.run(['systemctl','cat',u], capture_output=True).returncode==0:
        subprocess.run(['systemctl','restart',u], check=False)
        print('restarted', u)
print('DONE')
"""
    _, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY")
    print(stdout.read().decode("utf-8", errors="replace"))
    err = stderr.read().decode("utf-8", errors="replace")
    if err.strip():
        print(err, file=sys.stderr)
    code = stdout.channel.recv_exit_status()
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
