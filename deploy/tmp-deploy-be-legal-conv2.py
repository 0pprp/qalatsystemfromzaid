#!/usr/bin/env python3
"""Redeploy BE_Company only (LegalConversion EnsureSchema QUOTED_IDENTIFIER)."""
from __future__ import annotations

import hashlib
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
PUB = ROOT / "_publish_be_legal_conv2"
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
MARKER = b"SET QUOTED_IDENTIFIER ON"

REMOTE_PY = r'''
import hashlib, os, shutil, subprocess, tarfile, time
from pathlib import Path
stamp = __STAMP__
skip = set(__SKIP__)
admins = __ADMINS__
services = __SERVICES__
marker = __MARKER__
bx = Path('/tmp/be_legal_conv2_x_' + stamp)
if bx.exists():
    shutil.rmtree(bx)
bx.mkdir()
with tarfile.open('/tmp/be_legal_conv2_' + stamp + '.tgz') as t:
    t.extractall(bx)
data = (bx / 'BE_Company.dll').read_bytes()
print('TAR', len(data), hashlib.sha256(data).hexdigest()[:16], marker in data)
if marker not in data:
    raise SystemExit('marker missing')
for svc in services:
    subprocess.run(['systemctl', 'stop', svc], check=False)
time.sleep(2)
ok = 0
for name in admins:
    dest = Path('/opt/qalaa-runtime') / name
    if not dest.is_dir():
        print('SKIP', name); continue
    for root, dirs, files in os.walk(bx):
        for f in files:
            src = Path(root) / f
            rel = src.relative_to(bx)
            if rel.name in skip:
                continue
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    live = (dest / 'BE_Company.dll').read_bytes()
    mark = marker in live
    print('BE', name, mark)
    if mark:
        ok += 1
for svc in services:
    subprocess.run(['systemctl', 'start', svc], check=False)
time.sleep(2)
print('najaf', subprocess.getoutput('systemctl is-active qalaa-company-sharenewnajaf').strip())
print('OK', ok, '/', len(admins))
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
        ["dotnet", "publish", str(BE / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode
    dll = (PUB / "BE_Company.dll").read_bytes()
    if MARKER not in dll and MARKER.decode().encode("utf-16le") not in dll:
        # SET QUOTED_IDENTIFIER may be UTF16 in DLL strings
        if b"LegalConversionRequests" not in dll and "LegalConversionRequests".encode("utf-16le") not in dll:
            print("FATAL missing marker")
            return 3
        print("WARN quoted_identifier ascii missing; LegalConversion present")
    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_legal_conv2_{stamp}.tgz"
    if be_tar.exists():
        be_tar.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    remote = (
        REMOTE_PY
        .replace("__STAMP__", repr(stamp))
        .replace("__SKIP__", repr(sorted(SKIP)))
        .replace("__ADMINS__", repr(ADMINS))
        .replace("__SERVICES__", repr(SERVICES))
        .replace("__MARKER__", repr(b"LegalConversionRequests"))
    )
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_legal_conv2_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_be_legal_conv2_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_be_legal_conv2_{stamp}.py", timeout=900)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
