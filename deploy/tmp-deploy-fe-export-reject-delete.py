#!/usr/bin/env python3
"""Deploy FE dist (sales-manager reject/delete Excel export)."""
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
FE = ROOT / "FE_Company" / "dist"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not (FE / "index.html").exists():
        print("missing FE dist")
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_fe_export_rej_{stamp}.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in FE.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE)).replace("\\", "/"))
    print("TAR", tar_path.stat().st_size, flush=True)

    remote = dedent(
        f"""\
        import os, shutil, subprocess, tarfile
        from pathlib import Path
        stamp = {stamp!r}
        x = Path('/tmp/fe_export_rej_x_' + stamp)
        if x.exists():
            shutil.rmtree(x)
        x.mkdir()
        with tarfile.open('/tmp/fe_export_rej_' + stamp + '.tgz') as t:
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
        assets = Path('/opt/qalaa-runtime/AdminFrontend/assets')
        hits = []
        for p in assets.glob('sales-manager-requests*.js'):
            text = p.read_text(encoding='utf-8', errors='ignore')
            if 'exportRejectDelete' in text or 'طلبات_مرفوضة' in text:
                hits.append(p.name)
        print('HITS', hits)
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/fe_export_rej_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_fe_export_rej_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        stdin, stdout, stderr = c.exec_command(
            f"python3 /tmp/deploy_fe_export_rej_{stamp}.py", timeout=180
        )
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:500])
        code = stdout.channel.recv_exit_status()
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
