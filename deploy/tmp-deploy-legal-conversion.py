#!/usr/bin/env python3
"""Deploy BE_Company legal-conversion approval flow + FE AdminFrontend."""
from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import sys
import tarfile
import time
from pathlib import Path
from textwrap import dedent

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
BE = ROOT / "BE_Company"
FE = ROOT / "FE_Company"
PUB = ROOT / "_publish_be_legal_conv"
FE_DIST = FE / "dist"
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
MARKER = b"LegalConversionRequests"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUB.exists():
        shutil.rmtree(PUB)
    print("dotnet publish BE_Company...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode
    dll = (PUB / "BE_Company.dll").read_bytes()
    if MARKER not in dll and MARKER.decode().encode("utf-16le") not in dll:
        print("FATAL: LegalConversionRequests marker missing in BE_Company.dll")
        return 3
    print("BE publish ok", hashlib.sha256(dll).hexdigest()[:16], flush=True)

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
    if not (FE_DIST / "index.html").exists():
        print("FE dist missing")
        return 2
    print("FE build ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_legal_conv_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_legal_conv_{stamp}.tgz"
    for p in (be_tar, fe_tar):
        if p.exists():
            p.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))
    print(
        f"BE {be_tar.stat().st_size/1024/1024:.1f}MB FE {fe_tar.stat().st_size/1024/1024:.1f}MB",
        flush=True,
    )

    remote = dedent(
        f"""\
        import hashlib, os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({sorted(SKIP)!r})
        admins = {ADMINS!r}
        services = {SERVICES!r}
        marker = {MARKER!r}

        bx = Path('/tmp/be_legal_conv_x_' + stamp)
        fx = Path('/tmp/fe_legal_conv_x_' + stamp)
        for x in (bx, fx):
            if x.exists():
                shutil.rmtree(x)
            x.mkdir()
        with tarfile.open('/tmp/be_legal_conv_' + stamp + '.tgz') as t:
            t.extractall(bx)
        with tarfile.open('/tmp/fe_legal_conv_' + stamp + '.tgz') as t:
            t.extractall(fx)
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
                print('SKIP', name)
                continue
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
            print('BE', name, 'marker', mark)
            if mark:
                ok += 1
        for svc in services:
            subprocess.run(['systemctl', 'start', svc], check=False)
        time.sleep(2)
        print('najaf', subprocess.getoutput('systemctl is-active qalaa-company-sharenewnajaf').strip())

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
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('OK', ok, '/', len(admins))
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload BE tgz...", flush=True)
        sftp.put(str(be_tar), f"/tmp/be_legal_conv_{stamp}.tgz")
        print("upload FE tgz...", flush=True)
        sftp.put(str(fe_tar), f"/tmp/fe_legal_conv_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_legal_conv_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_legal_conv_{stamp}.py", timeout=900)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:2000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
