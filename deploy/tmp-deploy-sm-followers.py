#!/usr/bin/env python3
"""Deploy SM Followers tab: BE_Company + GW + FE + DelegateWeb (start/end slots)."""
from __future__ import annotations

import hashlib
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
SSH_PASS = os.environ["SSH_PASS"]
ROOT = Path(__file__).resolve().parent.parent
PUB_BE = ROOT / "_publish_be_sm_followers"
PUB_GW = ROOT / "_publish_gw_sm_followers"
PUB_DEL = ROOT / "_publish_delegate_sm_followers"
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


def publish(csproj: Path, out: Path) -> None:
    if out.exists():
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
    print("BE", len(be), hashlib.sha256(be).hexdigest()[:16],
          b"GetFollowerLifetimeTrendAsync" in be,
          b"SalesManagerFollowerQueryService" in be)

    print("publish GW...", flush=True)
    publish(ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj", PUB_GW)

    print("publish Delegate...", flush=True)
    publish(
        ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication" / "BE_DelegateWebApplication.csproj",
        PUB_DEL,
    )
    del_dll = (PUB_DEL / "BE_DelegateWebApplication.dll").read_bytes()
    print("DEL", len(del_dll), b"IsStartSlot" in del_dll)

    print("npm build FE...", flush=True)
    r = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode
    assets = list((FE / "dist" / "assets").glob("*followers*"))
    print("FE_ASSETS", [p.name for p in assets[:8]])

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_sm_followers_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_sm_followers_{stamp}.tgz"
    del_tar = ROOT / f"_del_sm_followers_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_sm_followers_{stamp}.tgz"
    make_tar(PUB_BE, be_tar, True)
    make_tar(PUB_GW, gw_tar, True)
    make_tar(PUB_DEL, del_tar, True)
    make_tar(FE / "dist", fe_tar, False)

    remote_py = dedent(
        f"""\
        import os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        admins = {ADMINS!r}

        def extract(tgz, dest_dir):
            dest_dir = Path(dest_dir)
            if dest_dir.exists():
                shutil.rmtree(dest_dir)
            dest_dir.mkdir(parents=True)
            with tarfile.open(tgz) as t:
                t.extractall(dest_dir)
            return dest_dir

        def copy_tree(src, dest):
            for root, dirs, files in os.walk(src):
                for f in files:
                    if f in skip:
                        continue
                    s = Path(root) / f
                    rel = s.relative_to(src)
                    d = dest / rel
                    d.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(s, d)

        be_x = extract(f'/tmp/be_sm_followers_{{stamp}}.tgz', f'/tmp/be_sm_followers_x_{{stamp}}')
        print('BE_TAR', (be_x / 'BE_Company.dll').stat().st_size)

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
                    except Exception:
                        pass
            rt = dest / 'runtimes'
            if rt.is_dir():
                shutil.rmtree(rt, ignore_errors=True)
            copy_tree(be_x, dest)
            print('UPDATED', name)

        demo = Path('/opt/qalaa-demo/backend')
        if demo.is_dir():
            copy_tree(be_x, demo)
            print('UPDATED_DEMO')

        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            unit = wd_to_unit.get(str(dest), '')
            if unit:
                subprocess.run(['systemctl', 'start', unit], check=False)
                print('STARTED', unit)
        subprocess.run(['systemctl', 'restart', 'qalaa-demo-backend.service'], check=False)

        gw_x = extract(f'/tmp/gw_sm_followers_{{stamp}}.tgz', f'/tmp/gw_sm_followers_x_{{stamp}}')
        for dest_s in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway']:
            dest = Path(dest_s)
            if not dest.is_dir():
                continue
            copy_tree(gw_x, dest)
            print('GW', dest_s)
        subprocess.run(['systemctl', 'restart', 'qalaa-sales-gateway'], check=False)
        subprocess.run(['systemctl', 'restart', 'qalaa-demo-sales-gateway.service'], check=False)

        del_x = extract(f'/tmp/del_sm_followers_{{stamp}}.tgz', f'/tmp/del_sm_followers_x_{{stamp}}')
        dirs = []
        for name in sorted(os.listdir('/opt/qalaa-runtime')):
            if not name.lower().startswith('delegate'):
                continue
            if 'backup' in name.lower() or name.endswith('.bak'):
                continue
            d = Path('/opt/qalaa-runtime') / name
            if (d / 'BE_DelegateWebApplication.dll').is_file():
                dirs.append(d)
        demo_f = Path('/opt/qalaa-demo/follower-backend')
        if (demo_f / 'BE_DelegateWebApplication.dll').is_file():
            dirs.append(demo_f)
        print('DEL_DIRS', len(dirs))
        for dest in dirs:
            copy_tree(del_x, dest)
            print('DEL', dest.name)
        # restart delegate units
        units = subprocess.getoutput('systemctl list-units --type=service --all --no-legend')
        n = 0
        for line in units.splitlines():
            u = line.split()[0] if line.strip() else ''
            if 'delegate' in u.lower() and u.endswith('.service'):
                subprocess.run(['systemctl', 'restart', u], check=False)
                n += 1
        subprocess.run(['systemctl', 'restart', 'qalaa-demo-follower-backend.service'], check=False)
        print('DEL_RESTART', n)

        fe_x = extract(f'/tmp/fe_sm_followers_{{stamp}}.tgz', f'/tmp/fe_sm_followers_x_{{stamp}}')
        for dest_s in ['/opt/qalaa-runtime/AdminFrontend', '/opt/qalaa-demo/frontend']:
            dest = Path(dest_s)
            if not dest.is_dir():
                continue
            copy_tree(fe_x, dest)
            print('FE', dest_s)
        subprocess.run(['systemctl', 'reload', 'nginx'], check=False)

        fe_js = list(Path('/opt/qalaa-runtime/AdminFrontend/assets').glob('*followers*'))
        print('FE_FOLLOWERS_JS', [p.name for p in fe_js[:6]])
        print('HAS_FOLLOWER_TREND', b'GetFollowerLifetimeTrendAsync' in (be_x / 'BE_Company.dll').read_bytes())
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_sm_followers_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_sm_followers_{stamp}.tgz")
        sftp.put(str(del_tar), f"/tmp/del_sm_followers_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_sm_followers_{stamp}.tgz")
        script = f"/tmp/deploy_sm_followers_{stamp}.py"
        with sftp.file(script, "w") as f:
            f.write(remote_py)
        sftp.close()
        _, out, err = c.exec_command(f"python3 {script}", timeout=900)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[-2000:])
    finally:
        c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
