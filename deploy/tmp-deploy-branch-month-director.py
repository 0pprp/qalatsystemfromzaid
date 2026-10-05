#!/usr/bin/env python3
"""Deploy BE_Company branch-month + GW director fanout + FE director-ratings."""
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
PUB_BE = ROOT / "_publish_be_branch_month"
PUB_GW = ROOT / "_publish_gw_branch_month"
FE_DIST = ROOT / "FE_Company" / "dist"
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
BE_MARKER = b"stats/branch-month"
GW_MARKER = b"stats/branch-month"


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
    if not (PUB_BE / "BE_Company.dll").exists():
        print("missing BE publish", PUB_BE)
        return 2
    if not (PUB_GW / "BE_SalesEmployee.dll").exists():
        print("missing GW publish", PUB_GW)
        return 2
    if not (FE_DIST / "index.html").exists():
        print("missing FE dist")
        return 2

    be = (PUB_BE / "BE_Company.dll").read_bytes()
    gw = (PUB_GW / "BE_SalesEmployee.dll").read_bytes()
    print("BE", len(be), hashlib.sha256(be).hexdigest()[:16], BE_MARKER in be)
    print("GW", len(gw), hashlib.sha256(gw).hexdigest()[:16], GW_MARKER in gw)
    if BE_MARKER not in be:
        print("FATAL: branch-month missing in BE_Company.dll")
        return 2
    if GW_MARKER not in gw:
        print("FATAL: branch-month missing in BE_SalesEmployee.dll")
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_branch_month_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_branch_month_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_branch_month_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(FE_DIST, fe_tar, False)

    remote_py = dedent(
        f"""\
        import hashlib, os, shutil, subprocess, tarfile
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        admins = {ADMINS!r}
        be_marker = {BE_MARKER!r}
        gw_marker = {GW_MARKER!r}

        def extract(tgz, dest_dir):
            dest_dir = Path(dest_dir)
            if dest_dir.exists():
                shutil.rmtree(dest_dir)
            dest_dir.mkdir(parents=True)
            with tarfile.open(tgz) as t:
                t.extractall(dest_dir)
            return dest_dir

        def copy_tree(src, dest, skip_names):
            for root, dirs, files in os.walk(src):
                for f in files:
                    if f in skip_names:
                        continue
                    s = Path(root) / f
                    rel = s.relative_to(src)
                    d = dest / rel
                    d.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(s, d)

        be_x = extract(f'/tmp/be_branch_month_{{stamp}}.tgz', f'/tmp/be_branch_month_x_{{stamp}}')
        data = (be_x / 'BE_Company.dll').read_bytes()
        print('BE_TAR', len(data), hashlib.sha256(data).hexdigest()[:16], be_marker in data)
        if be_marker not in data:
            raise SystemExit('be marker missing')

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

        targets = []
        for name in admins:
            d = Path('/opt/qalaa-runtime') / name
            if (d / 'BE_Company.dll').is_file():
                targets.append(d)
        for d in Path('/opt/qalaa-runtime').glob('Admin*'):
            if (d / 'BE_Company.dll').is_file() and d not in targets:
                if 'backup' in d.name.lower() or d.name.endswith('-old'):
                    continue
                targets.append(d)
        demo = Path('/opt/qalaa-demo/backend')
        if (demo / 'BE_Company.dll').is_file():
            targets.append(demo)

        restarted = set()
        for dest in targets:
            copy_tree(be_x, dest, skip)
            print('BE', dest)
            unit = wd_to_unit.get(str(dest))
            if unit and unit not in restarted:
                subprocess.run(['systemctl', 'restart', unit], check=False)
                restarted.add(unit)
                print('RESTART', unit)

        gw_x = extract(f'/tmp/gw_branch_month_{{stamp}}.tgz', f'/tmp/gw_branch_month_x_{{stamp}}')
        gwd = (gw_x / 'BE_SalesEmployee.dll').read_bytes()
        print('GW_TAR', len(gwd), gw_marker in gwd)
        if gw_marker not in gwd:
            raise SystemExit('gw marker missing')
        for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
            dest = Path(dest_s)
            if not dest.is_dir():
                print('SKIP_GW', dest_s)
                continue
            copy_tree(gw_x, dest, skip)
            print('GW', dest_s)
        subprocess.run(['systemctl', 'restart', 'qalaa-sales-gateway'], check=False)
        subprocess.run(['systemctl', 'restart', 'qalaa-demo-sales-gateway.service'], check=False)

        fe_x = extract(f'/tmp/fe_branch_month_{{stamp}}.tgz', f'/tmp/fe_branch_month_x_{{stamp}}')
        for fe_dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
            fe_dest = Path(fe_dest_s)
            if not fe_dest.is_dir():
                print('SKIP_FE', fe_dest_s)
                continue
            copy_tree(fe_x, fe_dest, set())
            print('FE', fe_dest_s)
        assets = Path('/opt/qalaa-runtime/AdminFrontend/assets')
        hits = [p.name for p in assets.glob('director-ratings*.js')]
        print('FE_DIRECTOR_JS', hits)
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
        sftp.put(str(be_tar), f"/tmp/be_branch_month_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_branch_month_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_branch_month_{stamp}.tgz")
        remote_py_path = f"/tmp/deploy_branch_month_{stamp}.py"
        with sftp.file(remote_py_path, "w") as f:
            f.write(remote_py)
        sftp.close()
        stdin, stdout, stderr = c.exec_command(f"python3 {remote_py_path}", timeout=600)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:1200])
        code = stdout.channel.recv_exit_status()
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
