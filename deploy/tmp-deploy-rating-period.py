#!/usr/bin/env python3
"""Deploy rating period/daily fix: BE_Company (all admins) + FE_Company + gateway."""
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
GW = ROOT / "BE_SalesEmployee"
PUB_BE = ROOT / "_publish_be_rating_period"
PUB_GW = ROOT / "_publish_gw_rating_period"
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
    "qalaa-company-shortnewkarakaqeel", "qalaa-company-shortnewrosafaaqeel", "qalaa-sales-gateway",
]


def publish(csproj: Path, out: Path) -> None:
    if out.exists():
        shutil.rmtree(out)
    r = subprocess.run(
        ["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(out), "--nologo"],
        capture_output=True,
        text=True,
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
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("publish BE_Company...", flush=True)
    publish(BE / "BE_Company.csproj", PUB_BE)
    print("publish gateway...", flush=True)
    publish(GW / "BE_SalesEmployee.csproj", PUB_GW)
    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_rating_{stamp}.tgz"
    gw_tar = ROOT / f"_publish_gw_rating_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_rating_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(FE / "dist", fe_tar, False)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_rating_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_rating_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_rating_{stamp}.tgz")
        sftp.close()

        admins = ",".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
extract=/tmp/be_rating_extract_$stamp
rm -rf "$extract" && mkdir -p "$extract"
tar -xzf /tmp/be_rating_$stamp.tgz -C "$extract"
for d in {admins}; do
  dest=/opt/qalaa-runtime/$d
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$extract"/ "$dest"/
    echo UPDATED $d
  fi
done
# demo admin if present
for d in /opt/qalaa-runtime/Admin*; do
  [ -d "$d" ] || continue
  base=$(basename "$d")
  case "$base" in *backup*|*old*) continue;; esac
  if grep -q DatabaseCompanyNajaf_DEMO "$d/appsettings.json" 2>/dev/null; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$extract"/ "$d"/
    echo UPDATED_DEMO $base
  fi
done

gw_extract=/tmp/gw_rating_extract_$stamp
rm -rf "$gw_extract" && mkdir -p "$gw_extract"
tar -xzf /tmp/gw_rating_$stamp.tgz -C "$gw_extract"
for dest in /opt/qalaa-runtime/SalesGateway /opt/qalaa-runtime/BE_SalesEmployee /opt/qalaa-runtime/sales-gateway; do
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$gw_extract"/ "$dest"/
    echo GW_UPDATED $dest
  fi
done

fe_dest=/opt/qalaa-runtime/FE_Company
if [ -d "$fe_dest" ] || [ -d /var/www/fe-company ] || [ -d /opt/qalaa-runtime/fe-company ]; then
  for dest in /opt/qalaa-runtime/FE_Company /var/www/fe-company /opt/qalaa-runtime/fe-company /var/www/html/fe-company; do
    if [ -d "$dest" ]; then
      rm -rf /tmp/fe_rating_extract_$stamp
      mkdir -p /tmp/fe_rating_extract_$stamp
      tar -xzf /tmp/fe_rating_$stamp.tgz -C /tmp/fe_rating_extract_$stamp
      rsync -a /tmp/fe_rating_extract_$stamp/ "$dest"/
      echo FE_UPDATED $dest
    fi
  done
fi

for u in {services}; do
  systemctl restart "$u" 2>/dev/null && echo RESTARTED $u || true
done
# demo units
systemctl list-units --type=service --all --no-legend | awk '{{print $1}}' | while read u; do
  case "$u" in *demo*|*Demo*|*8080*) systemctl restart "$u" 2>/dev/null && echo RESTARTED $u || true;; esac
done
echo DONE
"""
        stdin, stdout, stderr = c.exec_command(remote, timeout=600)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out[-4000:])
        if err.strip():
            print("STDERR", err[:1500])
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
