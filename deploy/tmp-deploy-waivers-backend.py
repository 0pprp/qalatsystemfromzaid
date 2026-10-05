#!/usr/bin/env python3
"""Deploy waiver backend: BE_Company (branch) + BE_SalesEmployee (gateway)."""
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
BE = ROOT / "BE_Company"
GW = ROOT / "BE_SalesEmployee"
PUB_BE = ROOT / "_publish_be_waivers"
PUB_GW = ROOT / "_publish_gw_waivers"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
BE_SERVICES = [
    "qalaa-company-sharenewdewania", "qalaa-company-sharenewkarbala", "qalaa-company-sharenewkot",
    "qalaa-company-sharenewmaysanl", "qalaa-company-sharenewmothana", "qalaa-company-sharenewmusol",
    "qalaa-company-sharenewnajaf", "qalaa-company-sharenewnasria", "qalaa-company-sharenewrbabil",
    "qalaa-company-sharenewrbasra", "qalaa-company-sharenewrbasranwar", "qalaa-company-sharenewrdeiala",
    "qalaa-company-sharenewrkarak", "qalaa-company-sharenewrkarkok", "qalaa-company-sharenewrosafa",
    "qalaa-company-shortnewkarakaqeel", "qalaa-company-shortnewrosafaaqeel",
]
BE_MARKER = b"WaiverApplyService"
GW_MARKER = b"CustomerWaiverService"

REMOTE = r'''
import hashlib, os, shutil, subprocess, tarfile, time
from pathlib import Path
stamp = __STAMP__
skip = set(__SKIP__)
admins = __ADMINS__
be_services = __BE_SERVICES__
be_marker = __BE_MARKER__
gw_marker = __GW_MARKER__

bx = Path('/tmp/be_waivers_x_' + stamp)
gx = Path('/tmp/gw_waivers_x_' + stamp)
for x in (bx, gx):
    if x.exists():
        shutil.rmtree(x)
    x.mkdir()
with tarfile.open('/tmp/be_waivers_' + stamp + '.tgz') as t:
    t.extractall(bx)
with tarfile.open('/tmp/gw_waivers_' + stamp + '.tgz') as t:
    t.extractall(gx)

be = (bx / 'BE_Company.dll').read_bytes()
gw = (gx / 'BE_SalesEmployee.dll').read_bytes()
print('BE', len(be), hashlib.sha256(be).hexdigest()[:16], 'waiver', be_marker in be)
print('GW', len(gw), hashlib.sha256(gw).hexdigest()[:16], 'waiver', gw_marker in gw)
if be_marker not in be:
    raise SystemExit('BE waiver marker missing')
if gw_marker not in gw:
    raise SystemExit('GW waiver marker missing')

for svc in be_services:
    subprocess.run(['systemctl', 'stop', svc], check=False)
subprocess.run(['systemctl', 'stop', 'qalaa-sales-gateway'], check=False)
subprocess.run(['systemctl', 'stop', 'qalaa-demo-sales-gateway'], check=False)
time.sleep(2)

n = 0
for name in admins:
    dest = Path('/opt/qalaa-runtime') / name
    if not dest.is_dir():
        print('SKIP', name); continue
    for root, dirs, files in os.walk(bx):
        for f in files:
            if f in skip: continue
            src = Path(root) / f
            rel = src.relative_to(bx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    n += 1
    print('BE', name, 'ok')
demo = Path('/opt/qalaa-demo/backend')
if demo.is_dir():
    for root, dirs, files in os.walk(bx):
        for f in files:
            if f in skip: continue
            src = Path(root) / f
            rel = src.relative_to(bx)
            dst = demo / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('BE demo')

for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
    dest = Path(dest_s)
    if not dest.is_dir():
        continue
    for root, dirs, files in os.walk(gx):
        for f in files:
            if f in skip: continue
            src = Path(root) / f
            rel = src.relative_to(gx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('GW', dest_s)

for svc in be_services:
    subprocess.run(['systemctl', 'start', svc], check=False)
subprocess.run(['systemctl', 'start', 'qalaa-sales-gateway'], check=False)
subprocess.run(['systemctl', 'start', 'qalaa-demo-sales-gateway'], check=False)
time.sleep(3)
print('najaf', subprocess.getoutput('systemctl is-active qalaa-company-sharenewnajaf').strip())
print('gw', subprocess.getoutput('systemctl is-active qalaa-sales-gateway').strip())
print('OK', n, '/', len(admins))
print('DONE')
'''


def publish(csproj: Path, out: Path) -> None:
    if out.exists():
        shutil.rmtree(out)
    r = subprocess.run(
        ["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(out),
         "--nologo", "--disable-build-servers"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        raise SystemExit(r.returncode)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("publish BE_Company...", flush=True)
    publish(BE / "BE_Company.csproj", PUB_BE)
    if BE_MARKER not in (PUB_BE / "BE_Company.dll").read_bytes():
        print("BE marker missing")
        return 3
    print("BE ok", flush=True)

    print("publish BE_SalesEmployee...", flush=True)
    publish(GW / "BE_SalesEmployee.csproj", PUB_GW)
    if GW_MARKER not in (PUB_GW / "BE_SalesEmployee.dll").read_bytes():
        print("GW marker missing")
        return 3
    print("GW ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_waivers_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_waivers_{stamp}.tgz"
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB_BE.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_BE)).replace("\\", "/"))
    with tarfile.open(gw_tar, "w:gz") as tar:
        for p in PUB_GW.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_GW)).replace("\\", "/"))

    remote = (
        REMOTE.replace("__STAMP__", repr(stamp))
        .replace("__SKIP__", repr(sorted(SKIP)))
        .replace("__ADMINS__", repr(ADMINS))
        .replace("__BE_SERVICES__", repr(BE_SERVICES))
        .replace("__BE_MARKER__", repr(BE_MARKER))
        .replace("__GW_MARKER__", repr(GW_MARKER))
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    print("ssh...", flush=True)
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(be_tar), f"/tmp/be_waivers_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_waivers_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_waivers_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_waivers_{stamp}.py", timeout=900)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
