#!/usr/bin/env python3
"""Deploy SalesGateway only — DM ratings board proxies (rating-customers + profile)."""
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
BE = ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj"
PUB = ROOT / "_publish_gw_dm_ratings"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
MARKER = b"StatsRatingCustomers"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUB.exists():
        shutil.rmtree(PUB)
    print("dotnet publish GW...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode

    dll = (PUB / "BE_SalesEmployee.dll").read_bytes()
    if MARKER not in dll and MARKER.decode().encode("utf-16le") not in dll:
        print("FATAL: StatsRatingCustomers missing from DLL")
        return 3
    print("GW publish ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    local_tar = ROOT / f"_gw_dm_ratings_{stamp}.tgz"
    if local_tar.exists():
        local_tar.unlink()
    with tarfile.open(local_tar, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    print(f"tar {local_tar.stat().st_size / 1024 / 1024:.1f}MB", flush=True)

    apply = f"""
import os, shutil, subprocess, tarfile, time
from pathlib import Path
stamp = {stamp!r}
skip = {sorted(SKIP)!r}
remote_tar = f'/opt/qalaa-updates/gw-dm-ratings-{{stamp}}.tgz'
extract = Path(f'/tmp/gw_dm_ratings_{{stamp}}')
if extract.exists():
    shutil.rmtree(extract)
extract.mkdir(parents=True)
with tarfile.open(remote_tar, 'r:gz') as t:
    t.extractall(extract)
data = (extract / 'BE_SalesEmployee.dll').read_bytes()
print('StatsRatingCustomers', b'StatsRatingCustomers' in data or 'StatsRatingCustomers'.encode('utf-16le') in data)
for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
    dest = Path(dest_s)
    if not dest.is_dir() or not (dest / 'BE_SalesEmployee.dll').is_file():
        print('SKIP', dest_s)
        continue
    for u in ('qalaa-sales-gateway', 'qalaa-demo-sales-gateway'):
        subprocess.run(['systemctl', 'stop', u], check=False)
    time.sleep(1)
    for root, _, files in os.walk(extract):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(extract)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print('UPDATED', dest_s)
for u in ('qalaa-sales-gateway', 'qalaa-demo-sales-gateway'):
    subprocess.run(['systemctl', 'start', u], check=False)
    print(u, subprocess.getoutput(f'systemctl is-active {{u}}'))
print('DONE')
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    try:
        c.exec_command("mkdir -p /opt/qalaa-updates")[1].channel.recv_exit_status()
        remote_tar = f"/opt/qalaa-updates/gw-dm-ratings-{stamp}.tgz"
        apply_path = f"/tmp/apply_gw_dm_ratings_{stamp}.py"
        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        with sftp.file(apply_path, "w") as f:
            f.write(apply)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 {apply_path}", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500], file=sys.stderr)
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
