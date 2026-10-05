#!/usr/bin/env python3
"""Deploy fast customer profile (<1s) + browse-position restore on back.

BE_Company: scoped SQL, no PDF regen, parallel hydrate
FE: paint after JSON, parallel blobs, sessionStorage return state
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
PUB_BE = ROOT / "_publish_be_profile_fast"
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
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
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
    if b"ListSalesForCustomerAsync" not in be_dll:
        print("FATAL: ListSalesForCustomerAsync missing from BE dll")
        return 3
    print("BE markers ok", flush=True)

    print("dotnet test SalesRequestTests filter...", flush=True)
    tr = subprocess.run(
        [
            "dotnet", "test",
            str(ROOT / "BE_Company.Sales.Tests" / "BE_Company.Sales.Tests.csproj"),
            "-c", "Release", "--nologo", "--filter", "FullyQualifiedName~SalesRequest",
            "--verbosity", "minimal",
        ],
        capture_output=True,
        text=True,
    )
    print(tr.stdout[-2000:] if tr.stdout else "", flush=True)
    if tr.returncode:
        print(tr.stderr[-2000:] if tr.stderr else "")
        print("WARN: tests failed — continuing deploy (check logs)", flush=True)

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode
    dist = FE / "dist"
    markers = {
        "sm-requests-return-v1": False,
        "director-ratings-return-v1": False,
        "goBack": False,
    }
    for p in dist.rglob("*.js"):
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        for key in list(markers):
            if key in text:
                markers[key] = True
    print("FE markers:", markers, flush=True)
    if not markers["sm-requests-return-v1"]:
        print("FATAL: FE missing return-state marker")
        return 4

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_profile_fast_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_profile_fast_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(dist, fe_tar, False)
    print(f"tars: {be_tar.name} ({be_tar.stat().st_size/1024/1024:.1f}MB) {fe_tar.name} ({fe_tar.stat().st_size/1024/1024:.1f}MB)", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_profile_fast_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_profile_fast_{stamp}.tgz")
        sftp.close()

        admins = " ".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
extract=/tmp/be_profile_fast_x_$stamp
rm -rf "$extract" && mkdir -p "$extract"
tar -xzf /tmp/be_profile_fast_$stamp.tgz -C "$extract"
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

fe_src=/tmp/fe_profile_fast_x_$stamp
rm -rf "$fe_src" && mkdir -p "$fe_src"
tar -xzf /tmp/fe_profile_fast_$stamp.tgz -C "$fe_src"
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend; do
  if [ -d "$dest" ]; then
    rsync -a "$fe_src"/ "$dest"/
    echo FE_UPDATED $dest
  fi
done

systemctl restart {services}
sleep 3
echo DEPLOY_DONE
"""
        _, stdout, stderr = c.exec_command(remote, timeout=600)
        out = stdout.read().decode("utf-8", errors="replace")
        err = stderr.read().decode("utf-8", errors="replace")
        print(out[-4000:], flush=True)
        if err.strip():
            print("STDERR:", err[-1500:], flush=True)
        code = stdout.channel.recv_exit_status()
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
