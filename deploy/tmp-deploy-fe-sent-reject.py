#!/usr/bin/env python3
"""Build FE_Company and deploy to AdminFrontend only (reject CTA for sent requests)."""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tarfile
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
FE = ROOT / "FE_Company"
DIST = FE / "dist"
TAR = ROOT / "_publish_fe_reject.tgz"
REMOTE_FE = "/opt/qalaa-runtime/AdminFrontend"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    print("using existing dist...", flush=True)
    if not DIST.is_dir():
        print("missing dist — run npm run build first")
        return 2
    if TAR.exists():
        TAR.unlink()
    with tarfile.open(TAR, "w:gz") as tar:
        for p in DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(DIST)).replace("\\", "/"))
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(TAR), "/tmp/fe_reject.tgz")
        sftp.close()
        script = f"""
set -e
TS=$(date +%Y%m%d_%H%M%S)
FE={REMOTE_FE}
test -d "$FE"
cp -a "$FE" "/opt/qalaa-runtime/AdminFrontend.bak-reject-$TS" || true
rm -rf /tmp/fe_reject_extract
mkdir -p /tmp/fe_reject_extract
tar -xzf /tmp/fe_reject.tgz -C /tmp/fe_reject_extract
# Keep mobile-updates symlink if present
rsync -a --delete --exclude 'mobile-updates' /tmp/fe_reject_extract/ "$FE"/
echo DEPLOYED "$FE"
ls "$FE"/index.html
"""
        stdin, stdout, stderr = c.exec_command(script, timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        print(out)
        if err.strip():
            print("STDERR", err[:800])
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
