#!/usr/bin/env python3
"""Fast deploy: SalesGateway + FE for الكل sales-requests fan-out."""
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
FE = ROOT / "FE_Company"
GW = ROOT / "BE_SalesEmployee"
PUB_GW = ROOT / "_publish_gw_requests_all"
SKIP = {"appsettings.json", "appsettings.Development.json", "appsettings.Production.json"}


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("publish gateway...", flush=True)
    if PUB_GW.exists():
        shutil.rmtree(PUB_GW)
    r = subprocess.run(
        ["dotnet", "publish", str(GW / "BE_SalesEmployee.csproj"), "-c", "Release", "-o", str(PUB_GW), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode
    dll = (PUB_GW / "BE_SalesEmployee.dll").read_bytes()
    if b"GetSalesRequestsPageFanoutAsync" not in dll:
        print("FATAL: fanout method missing from dll")
        return 3

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode
    dist = FE / "dist"
    text_all = ""
    for p in dist.rglob("*.js"):
        try:
            text_all += p.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            pass
    if "كل المحافظات" not in text_all and "pageSize" not in text_all:
        print("WARN: FE markers weak")
    if "حدد المحافظة أولاً لتحميل طلبات البيع بسرعة" in text_all:
        print("FATAL: old block-toast still in FE")
        return 4

    stamp = time.strftime("%Y%m%d_%H%M%S")
    gw_tar = ROOT / f"_publish_gw_req_all_{stamp}.tgz"
    fe_tar = ROOT / f"_publish_fe_req_all_{stamp}.tgz"
    for tar_path, src, skip in ((gw_tar, PUB_GW, True), (fe_tar, dist, False)):
        if tar_path.exists():
            tar_path.unlink()
        with tarfile.open(tar_path, "w:gz") as tar:
            for p in src.rglob("*"):
                if p.is_file() and (not skip or p.name not in SKIP):
                    tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(gw_tar), f"/tmp/gw_req_all_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_req_all_{stamp}.tgz")
        sftp.close()
        remote = f"""
set -e
stamp='{stamp}'
gw_x=/tmp/gw_req_all_x_$stamp
rm -rf "$gw_x" && mkdir -p "$gw_x"
tar -xzf /tmp/gw_req_all_$stamp.tgz -C "$gw_x"
for dest in /opt/qalaa-runtime/SalesGateway /opt/qalaa-demo/sales-gateway; do
  [ -d "$dest" ] || continue
  rsync -a --exclude appsettings.json --exclude appsettings.Development.json --exclude appsettings.Production.json "$gw_x"/ "$dest"/
  echo GW_UPDATED $dest
done
fe_x=/tmp/fe_req_all_x_$stamp
rm -rf "$fe_x" && mkdir -p "$fe_x"
tar -xzf /tmp/fe_req_all_$stamp.tgz -C "$fe_x"
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend; do
  [ -d "$dest" ] || continue
  rsync -a "$fe_x"/ "$dest"/
  echo FE_UPDATED $dest
done
systemctl restart qalaa-sales-gateway && echo RESTARTED_GW
systemctl reload nginx 2>/dev/null || true
echo DONE_REQ_ALL
"""
        _, stdout, stderr = c.exec_command(remote, timeout=300)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out)
        if err.strip():
            print("STDERR", err[-1500:])
        if code != 0 or "DONE_REQ_ALL" not in out or "GW_UPDATED" not in out or "FE_UPDATED" not in out:
            return code or 5
        print("DEPLOY_OK")
        return 0
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
