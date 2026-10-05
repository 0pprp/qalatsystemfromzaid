#!/usr/bin/env python3
"""Deploy FE AdminFrontend only (director ratings cache UX)."""
from __future__ import annotations

import os
import sys
import tarfile
import time
from pathlib import Path
from textwrap import dedent

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
FE_DIST = ROOT / "FE_Company" / "dist"


def make_tar(src: Path, tar_path: Path) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not (FE_DIST / "index.html").exists():
        print("missing FE dist")
        return 2
    stamp = time.strftime("%Y%m%d_%H%M%S")
    fe_tar = ROOT / f"_fe_director_cache_{stamp}.tgz"
    make_tar(FE_DIST, fe_tar)
    remote = dedent(
        f"""\
        import os, shutil, subprocess, tarfile
        from pathlib import Path
        stamp = {stamp!r}
        x = Path('/tmp/fe_director_cache_x_' + stamp)
        if x.exists():
            shutil.rmtree(x)
        x.mkdir()
        with tarfile.open('/tmp/fe_director_cache_' + stamp + '.tgz') as t:
            t.extractall(x)
        for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
            dest = Path(dest_s)
            if not dest.is_dir():
                print('SKIP', dest_s)
                continue
            for root, dirs, files in os.walk(x):
                for f in files:
                    src = Path(root) / f
                    rel = src.relative_to(x)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('FE', dest_s)
        hits = [p.name for p in Path('/opt/qalaa-runtime/AdminFrontend/assets').glob('director-ratings*.js')]
        print('JS', hits[-3:])
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('DONE')
        """
    )
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(fe_tar), f"/tmp/fe_director_cache_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_fe_director_cache_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_fe_director_cache_{stamp}.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
