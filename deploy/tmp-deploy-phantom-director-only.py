#!/usr/bin/env python3
"""Deploy phantom rating for director panel only: BE_Company + AdminFrontend (no APK, no Delegate)."""
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
FE = ROOT / "FE_Company"
PUB = ROOT / "_publish_be_phantom"
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
MARKER = b"PhantomSaleDate"
FE_MARKER = "التقييم الوهمي".encode("utf-8")


def make_tar(src: Path, tar_path: Path, skip_settings: bool) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if not p.is_file():
                continue
            if skip_settings and p.name in SKIP:
                continue
            tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    need_pub = True
    if (PUB / "BE_Company.dll").is_file():
        need_pub = MARKER not in (PUB / "BE_Company.dll").read_bytes()
    if need_pub:
        if PUB.exists():
            shutil.rmtree(PUB)
        print("dotnet publish BE_Company...", flush=True)
        r = subprocess.run(
            [
                "dotnet", "publish", str(BE / "BE_Company.csproj"),
                "-c", "Release", "-o", str(PUB), "--nologo", "--disable-build-servers",
            ],
            capture_output=True,
            text=True,
        )
        if r.returncode:
            print(r.stdout[-2000:], r.stderr[-2000:])
            return r.returncode
    dll = (PUB / "BE_Company.dll").read_bytes()
    if MARKER not in dll:
        print("Local publish missing PhantomSaleDate")
        return 3
    print("BE ok", flush=True)

    fe_hit = (
        any(FE_MARKER in p.read_bytes() for p in FE_DIST.rglob("*.js"))
        if FE_DIST.is_dir()
        else False
    )
    if not fe_hit:
        print("npm build FE...", flush=True)
        r2 = subprocess.run(
            ["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True
        )
        if r2.returncode:
            print(r2.stdout[-2000:], r2.stderr[-2000:])
            return r2.returncode
        fe_hit = any(FE_MARKER in p.read_bytes() for p in FE_DIST.rglob("*.js"))
    print(f"FE ok phantom_js={fe_hit}", flush=True)
    if not fe_hit:
        return 4

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_phantom_dir_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_phantom_dir_{stamp}.tgz"
    make_tar(PUB, be_tar, True)
    make_tar(FE_DIST, fe_tar, False)
    print(
        f"tars BE={be_tar.stat().st_size / 1e6:.1f}MB FE={fe_tar.stat().st_size / 1e6:.1f}MB",
        flush=True,
    )

    remote = f"""
import hashlib, os, shutil, subprocess, tarfile, time
from pathlib import Path
stamp = {stamp!r}
skip = set({sorted(SKIP)!r})
admins = {ADMINS!r}
services = {SERVICES!r}
marker = {MARKER!r}
fe_marker = {FE_MARKER!r}
bx = Path('/tmp/be_phantom_dir_x_' + stamp)
fx = Path('/tmp/fe_phantom_dir_x_' + stamp)
for x in (bx, fx):
    if x.exists():
        shutil.rmtree(x)
    x.mkdir()
with tarfile.open('/tmp/be_phantom_dir_' + stamp + '.tgz') as t:
    t.extractall(bx)
with tarfile.open('/tmp/fe_phantom_dir_' + stamp + '.tgz') as t:
    t.extractall(fx)
data = (bx / 'BE_Company.dll').read_bytes()
ok = marker in data
print('BE', len(data), hashlib.sha256(data).hexdigest()[:16], 'phantom', ok)
if not ok:
    raise SystemExit('PhantomSaleDate missing')
for svc in services:
    subprocess.run(['systemctl', 'stop', svc], check=False)
time.sleep(2)
n = 0
for name in admins:
    dest = Path('/opt/qalaa-runtime') / name
    if not dest.is_dir():
        print('SKIP', name)
        continue
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
dest = Path('/opt/qalaa-runtime/AdminFrontend')
if not dest.is_dir():
    raise SystemExit('AdminFrontend missing')
for root, dirs, files in os.walk(fx):
    for f in files:
        src = Path(root) / f
        rel = src.relative_to(fx)
        dst = dest / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
touch = dest / 'index.html'
if touch.is_file():
    touch.touch()
hit = False
for p in dest.rglob('*.js'):
    try:
        if fe_marker in p.read_bytes():
            hit = True
            break
    except Exception:
        pass
print('FE AdminFrontend phantom_label', hit)
subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
print('OK', n, '/', len(admins), 'fe', hit)
print('DONE')
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    sftp.put(str(be_tar), f"/tmp/be_phantom_dir_{stamp}.tgz")
    sftp.put(str(fe_tar), f"/tmp/fe_phantom_dir_{stamp}.tgz")
    sftp.close()
    _, out, err = c.exec_command(f"python3 - <<'PY'\n{remote}\nPY", timeout=900)
    print(out.read().decode("utf-8", "replace"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e[-2000:])
    code = out.channel.recv_exit_status()
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
