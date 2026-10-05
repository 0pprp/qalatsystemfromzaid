#!/usr/bin/env python3
"""Deploy GW (SM stats) + FE AdminFrontend from already-built publish folders."""
from __future__ import annotations

import hashlib
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
PUB_GW = ROOT / "_publish_gw_sm_ratings"
FE_DIST = ROOT / "FE_Company" / "dist"
SKIP = [
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
]
MARKER = b"stats/rating-customers"


def make_tar(src: Path, tar_path: Path, skip_settings: bool) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and (not skip_settings or p.name not in SKIP):
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not (PUB_GW / "BE_SalesEmployee.dll").exists():
        print("missing GW publish", PUB_GW)
        return 2
    if not (FE_DIST / "index.html").exists():
        print("missing FE dist")
        return 2

    gw = (PUB_GW / "BE_SalesEmployee.dll").read_bytes()
    print("GW", len(gw), hashlib.sha256(gw).hexdigest()[:16], MARKER in gw)
    if MARKER not in gw:
        print("FATAL: stats/rating-customers missing in GW")
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    gw_tar = ROOT / f"_gw_sm_ratings_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_sm_ratings_{stamp}.tgz"
    make_tar(PUB_GW, gw_tar, True)
    make_tar(FE_DIST, fe_tar, False)

    remote_py = dedent(
        f"""\
        import os, shutil, subprocess, tarfile
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        marker = {MARKER!r}

        def extract(tgz, dest_dir):
            dest_dir = Path(dest_dir)
            if dest_dir.exists():
                shutil.rmtree(dest_dir)
            dest_dir.mkdir(parents=True)
            with tarfile.open(tgz) as t:
                t.extractall(dest_dir)
            return dest_dir

        gw_x = extract(f'/tmp/gw_sm_ratings_{{stamp}}.tgz', f'/tmp/gw_sm_ratings_x_{{stamp}}')
        data = (gw_x / 'BE_SalesEmployee.dll').read_bytes()
        print('GW_TAR', len(data), marker in data)
        if marker not in data:
            raise SystemExit('gw marker missing')

        for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
            dest = Path(dest_s)
            if not dest.is_dir():
                print('SKIP', dest_s)
                continue
            for root, dirs, files in os.walk(gw_x):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(gw_x)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('GW', dest_s)
        subprocess.run(['systemctl', 'restart', 'qalaa-sales-gateway'], check=False)
        subprocess.run(['systemctl', 'restart', 'qalaa-demo-sales-gateway.service'], check=False)

        fe_x = extract(f'/tmp/fe_sm_ratings_{{stamp}}.tgz', f'/tmp/fe_sm_ratings_x_{{stamp}}')
        for fe_dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
            fe_dest = Path(fe_dest_s)
            if not fe_dest.is_dir():
                print('SKIP_FE', fe_dest_s)
                continue
            for root, dirs, files in os.walk(fe_x):
                for f in files:
                    src = Path(root) / f
                    rel = src.relative_to(fe_x)
                    dst = fe_dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('FE', fe_dest_s)
        assets = Path('/opt/qalaa-runtime/AdminFrontend/assets')
        hits = [p.name for p in assets.glob('sales-manager-ratings*.js')]
        print('FE_RATINGS_JS', hits)
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(gw_tar), f"/tmp/gw_sm_ratings_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_sm_ratings_{stamp}.tgz")
        remote_py_path = f"/tmp/deploy_gw_fe_sm_ratings_{stamp}.py"
        with sftp.file(remote_py_path, "w") as f:
            f.write(remote_py)
        sftp.close()
        stdin, stdout, stderr = c.exec_command(f"python3 {remote_py_path}", timeout=300)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:800])
        code = stdout.channel.recv_exit_status()
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
