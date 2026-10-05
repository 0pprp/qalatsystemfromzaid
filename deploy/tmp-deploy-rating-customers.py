#!/usr/bin/env python3
"""Deploy rating-customers drill-down: BE + GW + FE."""
from __future__ import annotations

import hashlib
import os
import subprocess
import sys
import tarfile
import time
from pathlib import Path
from textwrap import dedent

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]
ROOT = Path(__file__).resolve().parent.parent
PUB_BE = ROOT / "_publish_be_rating_cust"
PUB_GW = ROOT / "_publish_gw_rating_cust"
FE = ROOT / "FE_Company"
SKIP = [
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
]
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
MARKER = b"GetEmployeeRatingCustomersAsync"


def publish(csproj: Path, out: Path) -> None:
    if out.exists():
        import shutil
        shutil.rmtree(out)
    r = subprocess.run(
        ["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(out), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        raise SystemExit(r.returncode)


def make_tar(src: Path, tar_path: Path, skip_settings: bool) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and (not skip_settings or p.name not in SKIP):
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    print("publish BE...", flush=True)
    publish(ROOT / "BE_Company" / "BE_Company.csproj", PUB_BE)
    be = (PUB_BE / "BE_Company.dll").read_bytes()
    print("BE", len(be), hashlib.sha256(be).hexdigest()[:16], MARKER in be)
    if MARKER not in be:
        print("FATAL: marker missing in BE_Company.dll")
        return 2

    print("publish GW...", flush=True)
    publish(ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj", PUB_GW)
    gw = (PUB_GW / "BE_SalesEmployee.dll").read_bytes()
    print("GW", len(gw), b"rating-customers" in gw)

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_rating_cust_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_rating_cust_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_rating_cust_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(FE / "dist", fe_tar, False)

    remote_py = dedent(
        f"""\
        import hashlib, os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        admins = {ADMINS!r}
        marker = {MARKER!r}

        def extract(tgz, dest_dir):
            dest_dir = Path(dest_dir)
            if dest_dir.exists():
                shutil.rmtree(dest_dir)
            dest_dir.mkdir(parents=True)
            with tarfile.open(tgz) as t:
                t.extractall(dest_dir)
            return dest_dir

        be_x = extract(f'/tmp/be_rating_cust_{{stamp}}.tgz', f'/tmp/be_rating_cust_x_{{stamp}}')
        data = (be_x / 'BE_Company.dll').read_bytes()
        print('BE_TAR', len(data), marker in data)
        if marker not in data:
            raise SystemExit('marker missing on server extract')

        units_out = subprocess.getoutput('systemctl list-units --type=service --all --no-legend')
        wd_to_unit = {{}}
        for line in units_out.splitlines():
            u = line.split()[0] if line.strip() else ''
            if not u.endswith('.service'):
                continue
            if 'qalaa-company' not in u and 'demo-backend' not in u:
                continue
            wd = subprocess.getoutput(f'systemctl show {{u}} -p WorkingDirectory --value').strip()
            if wd:
                wd_to_unit[wd] = u

        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            unit = wd_to_unit.get(str(dest), '')
            if unit:
                subprocess.run(['systemctl', 'stop', unit], check=False)
        time.sleep(2)

        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            if not dest.is_dir():
                continue
            for p in list(dest.iterdir()):
                if p.name in skip:
                    continue
                if p.is_file() and (
                    p.suffix.lower() in ('.dll', '.pdb', '.so')
                    or p.name in ('BE_Company.deps.json', 'BE_Company.runtimeconfig.json', 'web.config')
                ):
                    try:
                        p.unlink()
                    except Exception as e:
                        print('skip_del', name, p.name, e)
            rt = dest / 'runtimes'
            if rt.is_dir():
                shutil.rmtree(rt, ignore_errors=True)
            for root, dirs, files in os.walk(be_x):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(be_x)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('UPDATED', name)

        demo = Path('/opt/qalaa-demo/backend')
        if demo.is_dir():
            for root, dirs, files in os.walk(be_x):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(be_x)
                    dst = demo / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('UPDATED_DEMO')

        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            unit = wd_to_unit.get(str(dest), '')
            if unit:
                subprocess.run(['systemctl', 'start', unit], check=False)
                print('STARTED', unit)
        subprocess.run(['systemctl', 'restart', 'qalaa-demo-backend.service'], check=False)

        gw_x = extract(f'/tmp/gw_rating_cust_{{stamp}}.tgz', f'/tmp/gw_rating_cust_x_{{stamp}}')
        for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
            dest = Path(dest_s)
            if not dest.is_dir():
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

        fe_x = extract(f'/tmp/fe_rating_cust_{{stamp}}.tgz', f'/tmp/fe_rating_cust_x_{{stamp}}')
        fe_dest = Path('/opt/qalaa-runtime/AdminFrontend')
        for root, dirs, files in os.walk(fe_x):
            for f in files:
                src = Path(root) / f
                rel = src.relative_to(fe_x)
                dst = fe_dest / rel
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dst)
        print('FE_UPDATED')
        fe_js = list(Path('/opt/qalaa-runtime/AdminFrontend/assets').glob('sales-manager-employees*.js'))
        print('FE_JS', [p.name for p in fe_js], any('rating-customers' in p.read_text(errors='ignore') for p in fe_js))
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_rating_cust_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_rating_cust_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_rating_cust_{stamp}.tgz")
        script = f"/tmp/deploy_rating_cust_{stamp}.py"
        with sftp.file(script, "w") as f:
            f.write(remote_py)
        sftp.close()
        _, out, err = c.exec_command(f"python3 {script}", timeout=600)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[-1500:])
        return out.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
