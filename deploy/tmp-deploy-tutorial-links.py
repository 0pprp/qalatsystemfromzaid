#!/usr/bin/env python3
"""Deploy Sales Gateway tutorial-links + FE_Company SM tab."""
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
ROOT = Path(__file__).resolve().parents[1]
PUB_GW = ROOT / "_publish_gw_tutorial"
FE = ROOT / "FE_Company"
DIST = FE / "dist"
SKIP = {"appsettings.json", "appsettings.Development.json", "appsettings.Production.json"}
REMOTE_FE = "/opt/qalaa-runtime/AdminFrontend"


def publish_gw() -> None:
    if PUB_GW.exists():
        shutil.rmtree(PUB_GW)
    r = subprocess.run(
        ["dotnet", "publish", str(ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj"),
         "-c", "Release", "-o", str(PUB_GW), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        raise SystemExit(r.returncode)


def build_fe() -> None:
    r = subprocess.run(
        ["npm", "run", "build"],
        cwd=str(FE),
        capture_output=True,
        text=True,
        shell=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        raise SystemExit(r.returncode)
    if not DIST.is_dir():
        raise SystemExit("FE dist missing")


def make_tar(src: Path, tar_path: Path, *, skip: set[str] | None = None) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and (not skip or p.name not in skip):
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    print("publish GW...", flush=True)
    publish_gw()
    print("build FE...", flush=True)
    build_fe()
    stamp = time.strftime("%Y%m%d_%H%M%S")
    gw_tar = ROOT / f"_gw_tutorial_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_tutorial_{stamp}.tgz"
    make_tar(PUB_GW, gw_tar, skip=SKIP)
    make_tar(DIST, fe_tar)
    print("tars ready", flush=True)

    remote = dedent(f"""\
        import os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp={stamp!r}
        skip=set({list(SKIP)!r})
        remote_fe={REMOTE_FE!r}

        def extract(tgz, dest):
            dest=Path(dest)
            shutil.rmtree(dest, ignore_errors=True)
            dest.mkdir(parents=True)
            tarfile.open(tgz).extractall(dest)
            return dest

        def copy_tree(src, dest):
            for root,_,files in os.walk(src):
                for f in files:
                    if f in skip: continue
                    s=Path(root)/f
                    d=dest/s.relative_to(src)
                    d.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(s,d)

        gw_x=extract(f'/tmp/gw_tutorial_{{stamp}}.tgz', f'/tmp/gw_tutorial_x_{{stamp}}')
        for dest_s in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
            dest=Path(dest_s)
            if dest.is_dir():
                copy_tree(gw_x, dest)
                print('GW', dest_s)
        subprocess.run(['systemctl','restart','qalaa-sales-gateway'], check=False)
        subprocess.run(['systemctl','restart','qalaa-demo-sales-gateway.service'], check=False)

        fe_x=extract(f'/tmp/fe_tutorial_{{stamp}}.tgz', f'/tmp/fe_tutorial_x_{{stamp}}')
        fe=Path(remote_fe)
        if fe.is_dir():
            bak=Path(f'/opt/qalaa-runtime/AdminFrontend.bak-tutorial-{{stamp}}')
            if not bak.exists():
                shutil.copytree(fe, bak, symlinks=True, dirs_exist_ok=True)
            subprocess.run(
                ['rsync','-a','--delete','--exclude','mobile-updates',
                 str(fe_x)+'/', str(fe)+'/'],
                check=False)
            print('FE', fe)

        dll=(gw_x/'BE_SalesEmployee.dll').read_bytes()
        print('HAS_TUTORIAL', b'MobileTutorialLinkService' in dll)
        print('DONE')
        """)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(gw_tar), f"/tmp/gw_tutorial_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_tutorial_{stamp}.tgz")
        script = f"/tmp/deploy_tutorial_{stamp}.py"
        with sftp.file(script, "w") as f:
            f.write(remote)
        sftp.close()
        _, out, err = c.exec_command(f"python3 {script}", timeout=600)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[-1500:])
    finally:
        c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
