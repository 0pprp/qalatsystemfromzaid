#!/usr/bin/env python3
"""Deploy FE only — director الكل load fix."""
from __future__ import annotations

import os
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
FE_DIST = ROOT / "FE_Company" / "dist"
MARKER = "120000"  # all-provinces timeout in sales-manager-requests chunk


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not FE_DIST.is_dir():
        print("dist missing — run npm build first")
        return 3

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_fe_dir_all_{stamp}.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))
    print("tar", tar_path.name, tar_path.stat().st_size, flush=True)

    remote = f'''
set -e
TGZ=/tmp/fe_dir_all_{stamp}.tgz
rm -rf /tmp/fe_dir_all_x_{stamp}
mkdir -p /tmp/fe_dir_all_x_{stamp}
tar -xzf "$TGZ" -C /tmp/fe_dir_all_x_{stamp}
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend; do
  if [ -d "$dest" ]; then
    rsync -a /tmp/fe_dir_all_x_{stamp}/ "$dest/"
    echo FE "$dest"
    stat -c "%y" "$dest/index.html"
  fi
done
# marker: all-provinces timeout
grep -l "{MARKER}" /opt/qalaa-runtime/AdminFrontend/assets/sales-manager-requests-*.js | head -1 || true
systemctl reload nginx 2>/dev/null || true
echo FE_OK
'''

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(tar_path), f"/tmp/fe_dir_all_{stamp}.tgz")
        sftp.close()
        _, out, err = c.exec_command(remote, timeout=300)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[:1200])
        return out.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
