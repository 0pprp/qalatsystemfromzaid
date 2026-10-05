#!/usr/bin/env python3
"""Deploy GW (director can use sales-manager APIs) + FE (طلبات البيع tab for المدير)."""
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
BE = ROOT / "BE_SalesEmployee"
FE = ROOT / "FE_Company"
PUB = ROOT / "_publish_gw_director_requests"
FE_DIST = FE / "dist"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
MARKER = b"SalesManagerOrDirector"

REMOTE_PY = r'''
import os, shutil, subprocess, tarfile, time
from pathlib import Path
stamp = __STAMP__
skip = set(__SKIP__)
marker = __MARKER__

fx = Path('/tmp/fe_dir_req_x_' + stamp)
gx = Path('/tmp/gw_dir_req_x_' + stamp)
for x in (fx, gx):
    if x.exists():
        shutil.rmtree(x)
    x.mkdir()
with tarfile.open('/tmp/fe_dir_req_' + stamp + '.tgz') as t:
    t.extractall(fx)
with tarfile.open('/tmp/gw_dir_req_' + stamp + '.tgz') as t:
    t.extractall(gx)
data = (gx / 'BE_SalesEmployee.dll').read_bytes()
print('GW', len(data), marker in data)
if marker not in data:
    raise SystemExit('marker missing')

for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
    dest = Path(dest_s)
    if not dest.is_dir():
        continue
    for root, dirs, files in os.walk(fx):
        for f in files:
            src = Path(root) / f
            rel = src.relative_to(fx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('FE', dest_s)

for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
    dest = Path(dest_s)
    if not dest.is_dir():
        print('SKIP GW', dest_s)
        continue
    subprocess.run(['systemctl', 'stop', 'qalaa-sales-gateway'], check=False)
    subprocess.run(['systemctl', 'stop', 'qalaa-demo-sales-gateway'], check=False)
    time.sleep(1)
    for root, dirs, files in os.walk(gx):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(gx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('GW', dest_s)

subprocess.run(['systemctl', 'start', 'qalaa-sales-gateway'], check=False)
subprocess.run(['systemctl', 'start', 'qalaa-demo-sales-gateway'], check=False)
subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
time.sleep(2)
print('gw', subprocess.getoutput('systemctl is-active qalaa-sales-gateway').strip())
print('DONE')
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUB.exists():
        shutil.rmtree(PUB)
    print("dotnet publish GW...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE / "BE_SalesEmployee.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode
    dll = (PUB / "BE_SalesEmployee.dll").read_bytes()
    if MARKER not in dll and MARKER.decode().encode("utf-16le") not in dll:
        print("FATAL: SalesManagerOrDirector missing")
        return 3
    print("GW publish ok", flush=True)

    print("npm build FE...", flush=True)
    r2 = subprocess.run(
        ["npm", "run", "build"],
        cwd=str(FE),
        capture_output=True,
        text=True,
        shell=True,
    )
    if r2.returncode:
        print(r2.stdout[-3000:], r2.stderr[-3000:])
        return r2.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    fe_tar = ROOT / f"_fe_dir_req_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_dir_req_{stamp}.tgz"
    for p in (fe_tar, gw_tar):
        if p.exists():
            p.unlink()
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))
    with tarfile.open(gw_tar, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))

    remote = (
        REMOTE_PY
        .replace("__STAMP__", repr(stamp))
        .replace("__SKIP__", repr(sorted(SKIP)))
        .replace("__MARKER__", repr(MARKER))
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(fe_tar), f"/tmp/fe_dir_req_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_dir_req_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_dir_req_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_dir_req_{stamp}.py", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
