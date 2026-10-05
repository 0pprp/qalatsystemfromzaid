#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import os
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUB_BE = ROOT / "_publish_be_hold_null_fix"
REMOTE_PY = Path(__file__).with_name("tmp_remote_deploy_hold_null.py")
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
MARKER = "ExceptionHoldStatus IS NOT NULL".encode("utf-16le")


def make_tar(src: Path, tar_path: Path) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    dll = PUB_BE / "BE_Company.dll"
    data = dll.read_bytes()
    print("BE", len(data), hashlib.sha256(data).hexdigest()[:16], MARKER in data)
    if MARKER not in data:
        print("FATAL marker missing")
        return 2
    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_hold_null_{stamp}.tgz"
    make_tar(PUB_BE, be_tar)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(be_tar), f"/tmp/be_hold_null_{stamp}.tgz")
        sftp.put(str(REMOTE_PY), f"/tmp/deploy_hold_null_{stamp}.py")
        sftp.close()
        _, stdout, stderr = c.exec_command(
            f"STAMP={stamp} python3 /tmp/deploy_hold_null_{stamp}.py", timeout=600
        )
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1200])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
