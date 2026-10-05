#!/usr/bin/env python3
"""Force-deploy BE rating fix + FE; verify PeriodAggregate on AdminNajaf."""
from __future__ import annotations

import os
import subprocess
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUB_BE = ROOT / "_publish_be_rating_fix"
FE = ROOT / "FE_Company"
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

    dll = PUB_BE / "BE_Company.dll"
    if not dll.is_file() or b"PeriodAggregate" not in dll.read_bytes():
        print("invalid publish", PUB_BE)
        return 3

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_rating_fix_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_rating_fix_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(FE / "dist", fe_tar, False)
    print(f"tars ready BE={be_tar.stat().st_size/1e6:.1f}MB FE={fe_tar.stat().st_size/1e6:.1f}MB", flush=True)

    remote_py = f"""
import os, shutil, subprocess, tarfile
from pathlib import Path

stamp = {stamp!r}
admins = {ADMINS!r}
services = {SERVICES!r}
skip = {sorted(SKIP)!r}

extract = Path(f'/tmp/be_rating_fix_extract_{{stamp}}')
if extract.exists():
    shutil.rmtree(extract)
extract.mkdir(parents=True)
with tarfile.open(f'/tmp/be_rating_fix_{{stamp}}.tgz') as tar:
    tar.extractall(extract)
data = (extract / 'BE_Company.dll').read_bytes()
print('REMOTE_HAS_PeriodAggregate', b'PeriodAggregate' in data, 'size', len(data))
if b'PeriodAggregate' not in data:
    raise SystemExit(4)

for name in admins:
    dest = Path('/opt/qalaa-runtime') / name
    if not dest.is_dir():
        continue
    for root, dirs, files in os.walk(extract):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(extract)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('UPDATED', name)

demo = Path('/opt/qalaa-demo/backend')
if demo.is_dir():
    for root, dirs, files in os.walk(extract):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(extract)
            dst = demo / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('UPDATED_DEMO')

fe_x = Path(f'/tmp/fe_rating_fix_extract_{{stamp}}')
if fe_x.exists():
    shutil.rmtree(fe_x)
fe_x.mkdir(parents=True)
with tarfile.open(f'/tmp/fe_rating_fix_{{stamp}}.tgz') as tar:
    tar.extractall(fe_x)
fe_dest = Path('/opt/qalaa-runtime/AdminFrontend')
for root, dirs, files in os.walk(fe_x):
    for f in files:
        src = Path(root) / f
        rel = src.relative_to(fe_x)
        dst = fe_dest / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
print('FE_UPDATED')

for u in services:
    r = subprocess.run(['systemctl', 'restart', u], capture_output=True, text=True)
    print(('RESTARTED' if r.returncode == 0 else 'FAIL'), u)
subprocess.run(['systemctl', 'restart', 'qalaa-demo-backend.service'], capture_output=True)
subprocess.run(['systemctl', 'reload', 'nginx'], capture_output=True)

najaf = Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll').read_bytes()
print('NAJAF_HAS_PeriodAggregate', b'PeriodAggregate' in najaf, 'size', len(najaf))
print('DONE')
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_rating_fix_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_rating_fix_{stamp}.tgz")
        remote_script = f"/tmp/deploy_rating_fix_{stamp}.py"
        with sftp.file(remote_script, "w") as f:
            f.write(remote_py)
        sftp.close()

        _, stdout, stderr = c.exec_command(f"python3 {remote_script}", timeout=600)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out)
        if err.strip():
            print("STDERR", err[-1500:])
        if "NAJAF_HAS_PeriodAggregate True" not in out:
            return 4
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
