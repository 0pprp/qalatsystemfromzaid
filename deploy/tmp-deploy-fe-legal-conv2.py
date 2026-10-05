#!/usr/bin/env python3
"""Redeploy FE AdminFrontend only (legal conversion UI polish)."""
from __future__ import annotations

import os
import subprocess
import sys
import tarfile
import time
from pathlib import Path
from textwrap import dedent

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
FE = ROOT / "FE_Company"
FE_DIST = FE / "dist"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("npm build FE...", flush=True)
    r = subprocess.run(
        ["npm", "run", "build"],
        cwd=str(FE),
        capture_output=True,
        text=True,
        shell=True,
    )
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    fe_tar = ROOT / f"_fe_legal_conv2_{stamp}.tgz"
    if fe_tar.exists():
        fe_tar.unlink()
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))

    remote = dedent(
        f"""\
        import os, shutil, subprocess, tarfile
        from pathlib import Path
        stamp = {stamp!r}
        fx = Path('/tmp/fe_legal_conv2_x_' + stamp)
        if fx.exists():
            shutil.rmtree(fx)
        fx.mkdir()
        with tarfile.open('/tmp/fe_legal_conv2_' + stamp + '.tgz') as t:
            t.extractall(fx)
        for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
            dest = Path(dest_s)
            if not dest.is_dir():
                continue
            for root, dirs, files in os.walk(fx):
                for f in files:
                    src = Path(root) / f
                    rel = src.relative_to(fx)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('FE', dest_s)
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(fe_tar), f"/tmp/fe_legal_conv2_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_fe_legal_conv2_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_fe_legal_conv2_{stamp}.py", timeout=300)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
