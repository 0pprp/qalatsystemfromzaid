#!/usr/bin/env python3
"""Deploy FE AdminFrontend + SalesGateway (director cache/back + file messages)."""
from __future__ import annotations

import os
import shutil
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
FE_DIST = ROOT / "FE_Company" / "dist"
BE = ROOT / "BE_SalesEmployee"
PUB = ROOT / "_publish_gw_director_cache"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def publish() -> None:
    if PUB.exists():
        shutil.rmtree(PUB)
    r = subprocess.run(
        ["dotnet", "publish", str(BE / "BE_SalesEmployee.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        raise SystemExit(r.returncode)
    dll = (PUB / "BE_SalesEmployee.dll").read_bytes()
    if b"customer-documents" not in dll:
        print("FATAL: publish missing director routes")
        raise SystemExit(3)
    print("GW publish ok", flush=True)


def make_tar(src: Path, tar_path: Path, skip: bool = False) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and (not skip or p.name not in SKIP):
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not (FE_DIST / "index.html").exists():
        print("missing FE dist — run npm build first")
        return 2

    print("publish gateway...", flush=True)
    publish()

    stamp = time.strftime("%Y%m%d_%H%M%S")
    fe_tar = ROOT / f"_fe_dir_cache_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_dir_cache_{stamp}.tgz"
    make_tar(FE_DIST, fe_tar)
    make_tar(PUB, gw_tar, skip=True)
    print(f"FE {fe_tar.stat().st_size/1024/1024:.1f}MB GW {gw_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    remote = dedent(
        f"""\
        import os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        # FE
        x = Path('/tmp/fe_dir_cache_x_' + stamp)
        if x.exists():
            shutil.rmtree(x)
        x.mkdir()
        with tarfile.open('/tmp/fe_dir_cache_' + stamp + '.tgz') as t:
            t.extractall(x)
        for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
            dest = Path(dest_s)
            if not dest.is_dir():
                print('SKIP FE', dest_s)
                continue
            for root, dirs, files in os.walk(x):
                for f in files:
                    src = Path(root) / f
                    rel = src.relative_to(x)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('FE', dest_s)
        hits = sorted(Path('/opt/qalaa-runtime/AdminFrontend/assets').glob('director-ratings*.js'))
        print('JS', [p.name for p in hits[-3:]])
        # Gateway
        g = Path('/tmp/gw_dir_cache_x_' + stamp)
        if g.exists():
            shutil.rmtree(g)
        g.mkdir()
        with tarfile.open('/tmp/gw_dir_cache_' + stamp + '.tgz') as t:
            t.extractall(g)
        dest = Path('/opt/qalaa-runtime/SalesGateway')
        bak = Path('/opt/qalaa-runtime/SalesGateway.bak-dir-cache-' + stamp)
        if bak.exists():
            shutil.rmtree(bak)
        # stop → swap dlls → start
        subprocess.run(['systemctl', 'stop', 'qalaa-sales-gateway'], check=False)
        time.sleep(1)
        if not bak.exists():
            shutil.copytree(dest, bak, dirs_exist_ok=True)
        for root, dirs, files in os.walk(g):
            for f in files:
                if f in {stamp!r}:
                    continue
                src = Path(root) / f
                rel = src.relative_to(g)
                if rel.name in ('appsettings.json', 'appsettings.Production.json', 'appsettings.Development.json'):
                    continue
                dst = dest / rel
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dst)
        subprocess.run(['systemctl', 'start', 'qalaa-sales-gateway'], check=False)
        time.sleep(2)
        st = subprocess.check_output(['systemctl', 'is-active', 'qalaa-sales-gateway'], text=True).strip()
        print('GW', st)
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(fe_tar), f"/tmp/fe_dir_cache_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_dir_cache_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_dir_cache_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_dir_cache_{stamp}.py", timeout=300)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
