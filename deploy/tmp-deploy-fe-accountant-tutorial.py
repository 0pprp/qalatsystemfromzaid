#!/usr/bin/env python3
"""Deploy FE accountant tutorial sidebar button + SalesGateway KnownApps(accountant)."""
from __future__ import annotations

import os
import shutil
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
GW_PROJ = ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj"
GW_PUB = ROOT / "_publish_gw_accountant_tutorial"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
FE_MARKER = "مشاهدة الفيديو التعليمي"
GW_MARKER = "لوحة المحاسب الرئيسي"


def make_tar(src: Path, tar_path: Path, skip_settings: bool = False) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if not p.is_file():
                continue
            if skip_settings and p.name in SKIP:
                continue
            tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


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
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode

    fe_js = list(FE_DIST.rglob("accountant-tutorial-video*.js"))
    nav_hit = any(
        FE_MARKER.encode("utf-8") in p.read_bytes()
        for p in FE_DIST.rglob("DefaultLayoutWithVerticalNav*.js")
    )
    if not fe_js:
        print("FATAL: accountant-tutorial-video chunk missing")
        return 3
    if not nav_hit:
        print("FATAL: sidebar marker missing in VerticalNav chunk")
        return 3
    print(f"FE build ok chunk={fe_js[0].name}", flush=True)

    if GW_PUB.exists():
        shutil.rmtree(GW_PUB)
    GW_PUB.mkdir(parents=True)
    print("dotnet publish SalesGateway...", flush=True)
    r2 = subprocess.run(
        [
            "dotnet",
            "publish",
            str(GW_PROJ),
            "-c",
            "Release",
            "-o",
            str(GW_PUB),
            "--nologo",
            "--disable-build-servers",
        ],
        capture_output=True,
        text=True,
    )
    if r2.returncode:
        print(r2.stdout[-3000:], r2.stderr[-3000:])
        return r2.returncode
    dll = (GW_PUB / "BE_SalesEmployee.dll").read_bytes()
    # .NET embeds UTF-16LE strings in assemblies
    if GW_MARKER.encode("utf-16-le") not in dll and GW_MARKER.encode("utf-8") not in dll:
        print("FATAL: gateway DLL missing accountant KnownApps title")
        return 4
    print("Gateway publish ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    fe_tar = ROOT / f"_fe_acct_tutorial_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_acct_tutorial_{stamp}.tgz"
    make_tar(FE_DIST, fe_tar)
    make_tar(GW_PUB, gw_tar, skip_settings=True)
    print(
        f"tars FE={fe_tar.stat().st_size / 1e6:.1f}MB GW={gw_tar.stat().st_size / 1e6:.1f}MB",
        flush=True,
    )

    remote = f"""
import os, shutil, subprocess, tarfile
from pathlib import Path
stamp = {stamp!r}
skip = set({sorted(SKIP)!r})
marker = {FE_MARKER!r}

fe_x = Path('/tmp/fe_acct_tutorial_x_' + stamp)
if fe_x.exists():
    shutil.rmtree(fe_x)
fe_x.mkdir()
with tarfile.open('/tmp/fe_acct_tutorial_' + stamp + '.tgz') as t:
    t.extractall(fe_x)
for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
    dest = Path(dest_s)
    if not dest.is_dir():
        print('SKIP_FE', dest_s)
        continue
    for root, _, files in os.walk(fe_x):
        for f in files:
            src = Path(root) / f
            rel = src.relative_to(fe_x)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    (dest / 'index.html').touch()
    print('FE', dest_s)
    hits = list((dest / 'assets').glob('accountant-tutorial-video*.js'))
    print('CHUNK', [h.name for h in hits[:2]])
    nav = list((dest / 'assets').glob('DefaultLayoutWithVerticalNav*.js'))
    print('NAV', any(marker.encode('utf-8') in p.read_bytes() for p in nav))

gw_x = Path('/tmp/gw_acct_tutorial_x_' + stamp)
if gw_x.exists():
    shutil.rmtree(gw_x)
gw_x.mkdir()
with tarfile.open('/tmp/gw_acct_tutorial_' + stamp + '.tgz') as t:
    t.extractall(gw_x)
dirs = [d for d in [
    '/opt/qalaa-runtime/SalesGateway',
    '/opt/qalaa-demo/sales-gateway',
    '/opt/qalaa-runtime/BE_SalesEmployee',
] if os.path.isfile(os.path.join(d, 'BE_SalesEmployee.dll'))]
print('GW_DIRS', len(dirs))
for dest in dirs:
    for root, _, files in os.walk(gw_x):
        for f in files:
            if f in skip:
                continue
            full = os.path.join(root, f)
            rel = os.path.relpath(full, gw_x)
            target = os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print('UPDATED', os.path.basename(dest))
for u in ('qalaa-sales-gateway', 'qalaa-demo-sales-gateway'):
    if subprocess.run(['systemctl', 'cat', u], capture_output=True).returncode == 0:
        subprocess.run(['systemctl', 'restart', u], check=False)
        print('restarted', u)
subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
print('FE_GW_OK')
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload FE...", flush=True)
        sftp.put(str(fe_tar), f"/tmp/fe_acct_tutorial_{stamp}.tgz")
        print("upload GW...", flush=True)
        sftp.put(str(gw_tar), f"/tmp/gw_acct_tutorial_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_acct_tutorial_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, out, err = c.exec_command(f"python3 /tmp/deploy_acct_tutorial_{stamp}.py", timeout=300)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[:1500])
        return out.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
