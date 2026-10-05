#!/usr/bin/env python3
"""Deploy FE — hide SM nav tabs."""
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
FE = ROOT / "FE_Company"
FE_DIST = FE / "dist"
MARKER = "مسارات المتابعين"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("npm build...", flush=True)
    r = subprocess.run(
        ["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_fe_sm_hide_{stamp}.tgz"
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))

    remote = f'''
set -e
rm -rf /tmp/fe_sm_hide_x_{stamp}
mkdir -p /tmp/fe_sm_hide_x_{stamp}
tar -xzf /tmp/fe_sm_hide_{stamp}.tgz -C /tmp/fe_sm_hide_x_{stamp}
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend; do
  [ -d "$dest" ] || continue
  rsync -a /tmp/fe_sm_hide_x_{stamp}/ "$dest/"
  touch "$dest/index.html"
  echo FE "$dest"
  grep -l "{MARKER}" "$dest"/assets/DefaultLayoutWithVerticalNav-*.js | head -1 || true
done
systemctl reload nginx 2>/dev/null || true
echo FE_OK
'''
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(tar_path), f"/tmp/fe_sm_hide_{stamp}.tgz")
        sftp.close()
        _, o, e = c.exec_command(remote, timeout=300)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
