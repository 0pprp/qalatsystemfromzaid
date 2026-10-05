#!/usr/bin/env python3
"""Deploy dashboard updates: BE_Company + SalesGateway + FE AdminFrontend.

Covers: director rating drill-down, KYC image timeout, sales-requests pagination,
employees tab rename.
"""
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
PUB_BE = ROOT / "_publish_be_dashboards_v1"
PUB_GW = ROOT / "_publish_gw_dashboards_v1"
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
    be_dll = (PUB_BE / "BE_Company.dll").read_bytes()
    if b"ListManagerPageAsync" not in be_dll and b"ListForManagerPageAsync" not in be_dll:
        # method names may be trimmed; check DTO marker
        if b"SalesRequestManagerListPageDTO" not in be_dll:
            print("WARN: paged list markers not obvious in BE dll")

    print("publish gateway...", flush=True)
    publish(GW / "BE_SalesEmployee.csproj", PUB_GW)
    gw_dll = (PUB_GW / "BE_SalesEmployee.dll").read_bytes()
    if b"NeedsLongFileTimeout" not in gw_dll:
        print("WARN: NeedsLongFileTimeout not found in gateway dll")
    if b"rating-customers" not in gw_dll and b"CompanyRatingCustomers" not in gw_dll:
        print("WARN: director rating-customers path not obvious in gateway dll")

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode
    dist = FE / "dist"
    markers = {
        "موظفين المبيعات": False,
        "تحميل المزيد": False,
        "حدد المحافظة لتحميل": False,
        "بحث في السيرفر": False,
    }
    for p in dist.rglob("*"):
        if not p.is_file() or p.suffix.lower() not in {".js", ".css", ".html"}:
            continue
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        for key in list(markers):
            if key in text:
                markers[key] = True
    print("FE markers:", markers, flush=True)
    if not markers["موظفين المبيعات"] or not markers["تحميل المزيد"]:
        print("FATAL: FE dist missing dashboard markers")
        return 3

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_dashboards_v1_{stamp}.tgz"
    gw_tar = ROOT / f"_publish_gw_dashboards_v1_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_dashboards_v1_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(dist, fe_tar, False)
    print(f"tars: {be_tar.name} {gw_tar.name} {fe_tar.name}", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_dash_v1_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_dash_v1_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_dash_v1_{stamp}.tgz")
        sftp.close()

        admins = " ".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
extract=/tmp/be_dash_v1_x_$stamp
rm -rf "$extract" && mkdir -p "$extract"
tar -xzf /tmp/be_dash_v1_$stamp.tgz -C "$extract"
for d in {admins}; do
  dest=/opt/qalaa-runtime/$d
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$extract"/ "$dest"/
    echo UPDATED_OK $d
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

gw_extract=/tmp/gw_dash_v1_x_$stamp
rm -rf "$gw_extract" && mkdir -p "$gw_extract"
tar -xzf /tmp/gw_dash_v1_$stamp.tgz -C "$gw_extract"
for dest in /opt/qalaa-runtime/SalesGateway /opt/qalaa-runtime/BE_SalesEmployee /opt/qalaa-runtime/sales-gateway /opt/qalaa-demo/sales-gateway; do
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$gw_extract"/ "$dest"/
    echo GW_UPDATED $dest
  fi
done

fe_src=/tmp/fe_dash_v1_x_$stamp
rm -rf "$fe_src" && mkdir -p "$fe_src"
tar -xzf /tmp/fe_dash_v1_$stamp.tgz -C "$fe_src"
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend /opt/qalaa-runtime/FE_Company /var/www/fe-company /opt/qalaa-runtime/fe-company; do
  if [ -d "$dest" ]; then
    rsync -a "$fe_src"/ "$dest"/
    echo FE_UPDATED $dest
  fi
done

for u in {services}; do
  systemctl restart "$u" 2>/dev/null && echo RESTARTED $u || true
done
systemctl reload nginx 2>/dev/null && echo NGINX_RELOADED || true
echo DONE_DASHBOARDS_V1
"""
        print("remote deploy...", flush=True)
        _stdin, stdout, stderr = c.exec_command(remote, timeout=900)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out)
        if err.strip():
            print("STDERR:", err[-2000:])
        if code != 0:
            print("remote exit", code)
            return code
        if "DONE_DASHBOARDS_V1" not in out:
            print("FATAL: missing DONE marker")
            return 4
        ok = out.count("UPDATED_OK")
        print(f"admins UPDATED_OK={ok}", flush=True)
        if ok < 10:
            print("FATAL: too few admins updated")
            return 5
        if "GW_UPDATED" not in out or "FE_UPDATED" not in out:
            print("FATAL: gateway or FE not updated")
            return 6
        print("DEPLOY_OK", flush=True)
        return 0
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
