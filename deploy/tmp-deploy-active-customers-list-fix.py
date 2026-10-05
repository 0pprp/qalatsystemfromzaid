#!/usr/bin/env python3
"""Deploy BE_Company only — rating-customers uses active cohort for list/follower/branch."""
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
PUB = ROOT / "_publish_be_active_customers_fix"
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
SERVICES = [
    "qalaa-company-sharenewdewania", "qalaa-company-sharenewkarbala", "qalaa-company-sharenewkot",
    "qalaa-company-sharenewmaysanl", "qalaa-company-sharenewmothana", "qalaa-company-sharenewmusol",
    "qalaa-company-sharenewnajaf", "qalaa-company-sharenewnasria", "qalaa-company-sharenewrbabil",
    "qalaa-company-sharenewrbasra", "qalaa-company-sharenewrbasranwar", "qalaa-company-sharenewrdeiala",
    "qalaa-company-sharenewrkarak", "qalaa-company-sharenewrkarkok", "qalaa-company-sharenewrosafa",
    "qalaa-company-shortnewkarakaqeel", "qalaa-company-shortnewrosafaaqeel",
]
MARKER = b"useActive"

REMOTE = r'''
import hashlib, os, shutil, subprocess, tarfile, time
from pathlib import Path
stamp = __STAMP__
skip = set(__SKIP__)
admins = __ADMINS__
services = __SERVICES__
marker = __MARKER__

bx = Path('/tmp/be_active_cust_fix_x_' + stamp)
if bx.exists():
    shutil.rmtree(bx)
bx.mkdir()
with tarfile.open('/tmp/be_active_cust_fix_' + stamp + '.tgz') as t:
    t.extractall(bx)
data = (bx / 'BE_Company.dll').read_bytes()
# IL may mangle local name; ActiveMinPercentExclusive proves active rating code is present
ok = b'ActiveMinPercentExclusive' in data
print('BE', len(data), hashlib.sha256(data).hexdigest()[:16], 'active', ok)
if not ok:
    raise SystemExit('marker missing')

for svc in services:
    subprocess.run(['systemctl', 'stop', svc], check=False)
time.sleep(2)
n = 0
for name in admins:
    dest = Path('/opt/qalaa-runtime') / name
    if not dest.is_dir():
        print('SKIP', name); continue
    for root, dirs, files in os.walk(bx):
        for f in files:
            if f in skip:
                continue
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
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(bx)
            dst = demo / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('BE demo')
for svc in services:
    subprocess.run(['systemctl', 'start', svc], check=False)
time.sleep(2)
print('najaf', subprocess.getoutput('systemctl is-active qalaa-company-sharenewnajaf').strip())
print('OK', n, '/', len(admins))
print('DONE')
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if PUB.exists():
        shutil.rmtree(PUB)
    print("dotnet publish...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB),
         "--nologo", "--disable-build-servers"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode
    print("BE ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_active_cust_fix_{stamp}.tgz"
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))

    remote = (
        REMOTE.replace("__STAMP__", repr(stamp))
        .replace("__SKIP__", repr(sorted(SKIP)))
        .replace("__ADMINS__", repr(ADMINS))
        .replace("__SERVICES__", repr(SERVICES))
        .replace("__MARKER__", repr(MARKER))
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    print("ssh...", flush=True)
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(be_tar), f"/tmp/be_active_cust_fix_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_active_cust_fix_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_active_cust_fix_{stamp}.py", timeout=900)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
