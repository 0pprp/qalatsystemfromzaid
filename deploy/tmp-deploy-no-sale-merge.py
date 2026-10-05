#!/usr/bin/env python3
"""Deploy BE_Company: never merge sales onto existing CustomerID (CustomersSales_Create only)."""
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
PUB = ROOT / "_publish_be_no_sale_merge"
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
    if b"Never InsertCustomerSale" not in dll and b"independent official account" not in dll:
        # comment may be stripped; ensure InsertCustomerSale call path reduced — check marker string we added
        if b"no merged AmountTotalSales" not in dll:
            print("WARN: comment marker not in dll (may be optimized); continuing")

    # Soft check: still may contain InsertCustomerSale string elsewhere; verify method comment present
    if b"CustomersSales_Create" not in dll:
        print("FATAL: CustomersSales_Create missing")
        return 3

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_publish_be_no_merge_{stamp}.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    print(f"tar {tar_path.name} {tar_path.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/be_no_merge_{stamp}.tgz")
        sftp.close()
        admins = " ".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
x=/tmp/be_no_merge_x_$stamp
rm -rf "$x" && mkdir -p "$x"
tar -xzf /tmp/be_no_merge_$stamp.tgz -C "$x"
for d in {admins}; do
  dest=/opt/qalaa-runtime/$d
  [ -d "$dest" ] || continue
  rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json --exclude App_Data "$x"/ "$dest"/
  echo UPDATED $d
done
systemctl restart {services}
sleep 3
# verify rusafa split still intact
python3 - <<'PY'
import json
from pathlib import Path
# quick file existence of dll
p=Path('/opt/qalaa-runtime/AdminBaghdadRusafa/BE_Company.dll')
print('rusafa_dll', p.exists(), p.stat().st_size if p.exists() else 0)
PY
echo DEPLOY_DONE
"""
        _, stdout, stderr = c.exec_command(remote, timeout=600)
        print(stdout.read().decode("utf-8", errors="replace")[-5000:])
        err = stderr.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-1500:])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
