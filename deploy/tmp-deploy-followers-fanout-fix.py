#!/usr/bin/env python3
"""Deploy gateway fan-out timeout fix + BE_Company follower live timeout."""
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
SSH_PASS = os.environ["SSH_PASS"]
ROOT = Path(__file__).resolve().parent.parent
PUB_BE = ROOT / "_publish_be_followers_fix"
PUB_GW = ROOT / "_publish_gw_followers_fix"
SKIP = {"appsettings.json", "appsettings.Development.json", "appsettings.Production.json"}
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
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        raise SystemExit(r.returncode)


def make_tar(src: Path, tar_path: Path) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    print("publish BE...", flush=True)
    publish(ROOT / "BE_Company" / "BE_Company.csproj", PUB_BE)
    print("publish GW...", flush=True)
    publish(ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj", PUB_GW)
    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_followers_fix_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_followers_fix_{stamp}.tgz"
    make_tar(PUB_BE, be_tar)
    make_tar(PUB_GW, gw_tar)
    print("tars ready", flush=True)

    remote = dedent(f"""\
        import os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp={stamp!r}
        skip=set({list(SKIP)!r})
        admins={ADMINS!r}

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

        be_x=extract(f'/tmp/be_followers_fix_{{stamp}}.tgz', f'/tmp/be_followers_fix_x_{{stamp}}')
        units_out=subprocess.getoutput('systemctl list-units --type=service --all --no-legend')
        wd_to_unit={{}}
        for line in units_out.splitlines():
            u=line.split()[0] if line.strip() else ''
            if not u.endswith('.service'): continue
            if 'qalaa-company' not in u and 'demo-backend' not in u: continue
            wd=subprocess.getoutput(f'systemctl show {{u}} -p WorkingDirectory --value').strip()
            if wd: wd_to_unit[wd]=u
        for name in admins:
            dest=Path('/opt/qalaa-runtime')/name
            unit=wd_to_unit.get(str(dest),'')
            if unit: subprocess.run(['systemctl','stop',unit], check=False)
        time.sleep(2)
        for name in admins:
            dest=Path('/opt/qalaa-runtime')/name
            if not dest.is_dir(): continue
            copy_tree(be_x, dest)
            print('BE', name)
        demo=Path('/opt/qalaa-demo/backend')
        if demo.is_dir():
            copy_tree(be_x, demo)
            print('BE_DEMO')
        for name in admins:
            dest=Path('/opt/qalaa-runtime')/name
            unit=wd_to_unit.get(str(dest),'')
            if unit: subprocess.run(['systemctl','start',unit], check=False)
        subprocess.run(['systemctl','restart','qalaa-demo-backend.service'], check=False)

        gw_x=extract(f'/tmp/gw_followers_fix_{{stamp}}.tgz', f'/tmp/gw_followers_fix_x_{{stamp}}')
        for dest_s in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway']:
            dest=Path(dest_s)
            if dest.is_dir():
                copy_tree(gw_x, dest)
                print('GW', dest_s)
        subprocess.run(['systemctl','restart','qalaa-sales-gateway'], check=False)
        subprocess.run(['systemctl','restart','qalaa-demo-sales-gateway.service'], check=False)
        dll=(gw_x/'BE_SalesEmployee.dll').read_bytes()
        print('HAS_LONGLIST', b'LongListBranchTimeout' in dll)
        print('DONE')
        """)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_followers_fix_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_followers_fix_{stamp}.tgz")
        script = f"/tmp/deploy_followers_fix_{stamp}.py"
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
