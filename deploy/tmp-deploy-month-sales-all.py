#!/usr/bin/env python3
"""Deploy BE_Company (month sales = all sales in month) + FE director labels."""
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
BE = ROOT / "BE_Company"
FE = ROOT / "FE_Company"
PUB = ROOT / "_publish_be_month_sales_all"
FE_DIST = FE / "dist"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
SERVICES = [
    "qalaa-company-sharenewdewania", "qalaa-company-sharenewkarbala", "qalaa-company-sharenewkot",
    "qalaa-company-sharenewmaysanl", "qalaa-company-sharenewmothana", "qalaa-company-sharenewmusol",
    "qalaa-company-sharenewnajaf", "qalaa-company-sharenewnasria", "qalaa-company-sharenewrbabil",
    "qalaa-company-sharenewrbasra", "qalaa-company-sharenewrbasranwar", "qalaa-company-sharenewrdeiala",
    "qalaa-company-sharenewrkarak", "qalaa-company-sharenewrkarkok", "qalaa-company-sharenewrosafa",
    "qalaa-company-shortnewkarakaqeel", "qalaa-company-shortnewrosafaaqeel",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUB.exists():
        shutil.rmtree(PUB)
    print("dotnet publish BE_Company...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(BE / "BE_Company.csproj"), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode
    dll = (PUB / "BE_Company.dll").read_bytes()
    marker = "DocumentsReady".encode("utf-16-le")
    if marker not in dll and b"DocumentsReady" not in dll:
        print("FATAL: draft-enrich month SQL marker missing")
        return 3
    print("BE publish ok", flush=True)

    print("npm build FE...", flush=True)
    r2 = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r2.returncode:
        print(r2.stdout[-2500:], r2.stderr[-2500:])
        return r2.returncode
    if not (FE_DIST / "index.html").exists():
        print("FE dist missing")
        return 2
    print("FE build ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_month_all_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_month_all_{stamp}.tgz"
    for p in (be_tar, fe_tar):
        if p.exists():
            p.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))
    print(f"BE {be_tar.stat().st_size/1024/1024:.1f}MB FE {fe_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    admins = repr(ADMINS)
    services = repr(SERVICES)
    remote = dedent(
        f"""\
        import os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        admins = {admins}
        services = {services}
        bx = Path('/tmp/be_month_all_x_' + stamp)
        fx = Path('/tmp/fe_month_all_x_' + stamp)
        for x in (bx, fx):
            if x.exists():
                shutil.rmtree(x)
            x.mkdir()
        with tarfile.open('/tmp/be_month_all_' + stamp + '.tgz') as t:
            t.extractall(bx)
        with tarfile.open('/tmp/fe_month_all_' + stamp + '.tgz') as t:
            t.extractall(fx)
        for svc in services:
            subprocess.run(['systemctl', 'stop', svc], check=False)
        time.sleep(2)
        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            if not dest.is_dir():
                print('SKIP', name)
                continue
            for root, dirs, files in os.walk(bx):
                for f in files:
                    src = Path(root) / f
                    rel = src.relative_to(bx)
                    if rel.name in ('appsettings.json','appsettings.Production.json','appsettings.Development.json'):
                        continue
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('BE', name)
        for svc in services:
            subprocess.run(['systemctl', 'start', svc], check=False)
        time.sleep(3)
        print('najaf', subprocess.check_output(['systemctl','is-active','qalaa-company-sharenewnajaf'], text=True).strip())
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
        # verify najaf month count
        import base64, json, urllib.request
        pid = subprocess.check_output(['systemctl','show','qalaa-company-sharenewnajaf','-p','MainPID','--value'], text=True).strip()
        env = Path(f'/proc/{{pid}}/environ').read_bytes().split(b'\\0')
        key = ''
        for item in env:
            if item.startswith(b'SalesEmployee__GatewayKey='):
                key = item.split(b'=',1)[1].decode()
        headers = {{'X-Sales-Gateway-Key': key, 'X-Sales-Manager-Name-B64': base64.b64encode(b'SalesManager').decode()}}
        req = urllib.request.Request('http://127.0.0.1:5301/api/sales-manager/stats/branch-month', headers=headers)
        with urllib.request.urlopen(req, timeout=90) as resp:
            raw = json.loads(resp.read().decode())
            cur = raw.get('current') or raw.get('Current') or {{}}
            print('VERIFY_NEW', cur.get('newCustomersCount') or cur.get('NewCustomersCount'))
            print('VERIFY_ZERO', cur.get('zeroedCustomersCount') or cur.get('ZeroedCustomersCount'))
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_month_all_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_month_all_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_month_all_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_month_all_{stamp}.py", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:2000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
