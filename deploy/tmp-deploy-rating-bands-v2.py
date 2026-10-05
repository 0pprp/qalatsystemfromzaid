#!/usr/bin/env python3
"""Deploy new rating bands (متلكئ/سيء, no قانونية): BE_Company + gateway + FE AdminFrontend."""
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
DELEGATE = ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication"
PUB_BE = ROOT / "_publish_be_rating_v2"
PUB_GW = ROOT / "_publish_gw_rating_v2"
PUB_DELEGATE = ROOT / "_publish_delegate_rating_v2"
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
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("publish BE_Company...", flush=True)
    publish(BE / "BE_Company.csproj", PUB_BE)
    dll = (PUB_BE / "BE_Company.dll").read_bytes()
    if "BadCount".encode("ascii") not in dll and b"BadCount" not in dll:
        # property name may be mangled; check متلكئ UTF-16LE or Arabic label bytes
        if "متلكئ".encode("utf-8") not in dll and "سيء".encode("utf-8") not in dll:
            print("WARN: Arabic labels not found as UTF-8 in dll (may still be OK)")
    print("publish gateway...", flush=True)
    publish(GW / "BE_SalesEmployee.csproj", PUB_GW)
    print("publish BE_Delegate...", flush=True)
    publish(DELEGATE / "BE_DelegateWebApplication.csproj", PUB_DELEGATE)

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode
    dist = FE / "dist"
    # Sanity: built bundle must contain new labels
    found_mutalaki = False
    found_sayi = False
    found_marfud = False
    for p in dist.rglob("*"):
        if not p.is_file():
            continue
        if p.suffix.lower() not in {".js", ".css", ".html", ".vue"}:
            continue
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        if "متلكئ" in text:
            found_mutalaki = True
        if "سيء" in text:
            found_sayi = True
        if "مرفوض" in text and "bucket" in text.lower():
            found_marfud = True
    print(f"FE labels: متلكئ={found_mutalaki} سيء={found_sayi}", flush=True)
    if not found_mutalaki or not found_sayi:
        print("FATAL: FE dist missing new rating labels")
        return 3

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_rating_v2_{stamp}.tgz"
    gw_tar = ROOT / f"_publish_gw_rating_v2_{stamp}.tgz"
    de_tar = ROOT / f"_publish_delegate_rating_v2_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_rating_v2_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(PUB_DELEGATE, de_tar, True)
    make_tar(dist, fe_tar, False)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_rating_v2_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_rating_v2_{stamp}.tgz")
        sftp.put(str(de_tar), f"/tmp/de_rating_v2_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_rating_v2_{stamp}.tgz")
        sftp.close()

        admins = " ".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
extract=/tmp/be_rating_v2_x_$stamp
rm -rf "$extract" && mkdir -p "$extract"
tar -xzf /tmp/be_rating_v2_$stamp.tgz -C "$extract"
for d in {admins}; do
  dest=/opt/qalaa-runtime/$d
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$extract"/ "$dest"/
    echo UPDATED $d
  else
    echo MISSING $d
  fi
done
for d in /opt/qalaa-runtime/Admin*; do
  [ -d "$d" ] || continue
  base=$(basename "$d")
  case "$base" in *backup*|*old*) continue;; esac
  if grep -q DatabaseCompanyNajaf_DEMO "$d/appsettings.json" 2>/dev/null; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$extract"/ "$d"/
    echo UPDATED_DEMO $base
  fi
done

gw_extract=/tmp/gw_rating_v2_x_$stamp
rm -rf "$gw_extract" && mkdir -p "$gw_extract"
tar -xzf /tmp/gw_rating_v2_$stamp.tgz -C "$gw_extract"
for dest in /opt/qalaa-runtime/SalesGateway /opt/qalaa-runtime/BE_SalesEmployee /opt/qalaa-runtime/sales-gateway /opt/qalaa-demo/sales-gateway; do
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$gw_extract"/ "$dest"/
    echo GW_UPDATED $dest
  fi
done

de_extract=/tmp/de_rating_v2_x_$stamp
rm -rf "$de_extract" && mkdir -p "$de_extract"
tar -xzf /tmp/de_rating_v2_$stamp.tgz -C "$de_extract"
for dest in /opt/qalaa-runtime/Delegate* /opt/qalaa-runtime/BE_Delegate* /opt/qalaa-runtime/*delegate*; do
  [ -d "$dest" ] || continue
  case "$dest" in *backup*|*old*) continue;; esac
  if [ -f "$dest/BE_DelegateWebApplication.dll" ] || [ -f "$dest/appsettings.json" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$de_extract"/ "$dest"/
    echo DELEGATE_UPDATED $dest
  fi
done

fe_src=/tmp/fe_rating_v2_x_$stamp
rm -rf "$fe_src" && mkdir -p "$fe_src"
tar -xzf /tmp/fe_rating_v2_$stamp.tgz -C "$fe_src"
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend /opt/qalaa-runtime/FE_Company /var/www/fe-company /opt/qalaa-runtime/fe-company; do
  if [ -d "$dest" ]; then
    rsync -a "$fe_src"/ "$dest"/
    echo FE_UPDATED $dest
  fi
done

for u in {services}; do
  systemctl restart "$u" 2>/dev/null && echo RESTARTED $u || true
done
systemctl list-units --type=service --all --no-legend | awk '{{print $1}}' | while read u; do
  case "$u" in *demo*|*Demo*|*8080*|*delegate*|*Delegate*) systemctl restart "$u" 2>/dev/null && echo RESTARTED $u || true;; esac
done
systemctl reload nginx 2>/dev/null || true
echo DONE
"""
        stdin, stdout, stderr = c.exec_command(remote, timeout=900)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out[-5000:])
        if err.strip():
            print("STDERR", err[:2000])
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
