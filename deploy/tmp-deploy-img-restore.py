#!/usr/bin/env python3
"""Deploy BE ReadFile recovery + FE image timeout/type relax after App_Data restore."""
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
PUB_BE = ROOT / "_publish_be_img_restore"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}

ADMIN_DIRS = [
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


def publish(csproj: Path, out: Path) -> None:
    if out.exists():
        shutil.rmtree(out)
    r = subprocess.run(
        ["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(out), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        raise SystemExit(r.returncode)


def make_tar(src: Path, tar_path: Path, skip: bool) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and (not skip or p.name not in SKIP):
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("publish BE...", flush=True)
    publish(BE / "BE_Company.csproj", PUB_BE)
    dll = (PUB_BE / "BE_Company.dll").read_bytes()
    if b"FindCustomerDocOnDisk" not in dll:
        print("FATAL: FindCustomerDocOnDisk missing")
        return 3

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_img_restore_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_img_restore_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(FE / "dist", fe_tar, False)
    print(f"tars ready {be_tar.name} {fe_tar.name}", flush=True)

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = client.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_img_restore_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_img_restore_{stamp}.tgz")
        sftp.close()
        admins = " ".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
extract=/tmp/be_img_restore_x_$stamp
rm -rf "$extract" && mkdir -p "$extract"
tar -xzf /tmp/be_img_restore_$stamp.tgz -C "$extract"
for d in {admins}; do
  dest=/opt/qalaa-runtime/$d
  [ -d "$dest" ] || continue
  rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json --exclude App_Data "$extract"/ "$dest"/
  echo UPDATED $d
done
fe_src=/tmp/fe_img_restore_x_$stamp
rm -rf "$fe_src" && mkdir -p "$fe_src"
tar -xzf /tmp/fe_img_restore_$stamp.tgz -C "$fe_src"
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend; do
  [ -d "$dest" ] || continue
  rsync -a "$fe_src"/ "$dest"/
  echo FE_UPDATED $dest
done
systemctl restart {services}
sleep 2
# prove files still present after deploy (App_Data excluded)
ls -la /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs/2.jpg /opt/qalaa-runtime/AdminNajaf/App_Data/sales/2/shop.jpg
echo DEPLOY_DONE
"""
        _, stdout, stderr = client.exec_command(remote, timeout=600)
        print(stdout.read().decode("utf-8", errors="replace")[-4000:])
        err = stderr.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return stdout.channel.recv_exit_status()
    finally:
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
