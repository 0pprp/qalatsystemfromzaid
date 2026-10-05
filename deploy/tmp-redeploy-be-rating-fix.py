#!/usr/bin/env python3
"""Re-deploy BE_Company only to all Admin* (fix space-separated loop)."""
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
PUB_BE = ROOT / "_publish_be_rating_v2"
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

    if PUB_BE.exists():
        shutil.rmtree(PUB_BE)
    print("publish BE...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB_BE), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode

    dll = (PUB_BE / "BE_Company.dll").read_bytes()
    print("local BadCount", b"BadCount" in dll, "size", len(dll), flush=True)
    if b"BadCount" not in dll:
        print("FATAL local dll missing BadCount")
        return 3

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_publish_be_fix_{stamp}.tgz"
    if be_tar.exists():
        be_tar.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB_BE.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_BE)).replace("\\", "/"))

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_fix_{stamp}.tgz")
        sftp.close()
        admins = " ".join(ADMIN_DIRS)
        services = " ".join(SERVICES)
        remote = f"""
set -e
stamp='{stamp}'
extract=/tmp/be_fix_x_$stamp
rm -rf "$extract" && mkdir -p "$extract"
tar -xzf /tmp/be_fix_$stamp.tgz -C "$extract"
test -f "$extract/BE_Company.dll"
grep -a -q BadCount "$extract/BE_Company.dll" && echo TAR_HAS_BadCount
for d in {admins}; do
  dest=/opt/qalaa-runtime/$d
  if [ -d "$dest" ]; then
    rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$extract"/ "$dest"/
    if grep -a -q BadCount "$dest/BE_Company.dll"; then echo UPDATED_OK $d; else echo UPDATED_BAD $d; fi
  else
    echo MISSING $d
  fi
done
for u in {services}; do
  systemctl restart "$u" 2>/dev/null && echo RESTARTED $u || true
done
echo DONE
"""
        i, o, e = c.exec_command(remote, timeout=600)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
