#!/usr/bin/env python3
"""Deploy payment-summary fallback + official sale item guard (BE only)."""
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
PUB_BE = ROOT / "_publish_be_pay_summary"
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
MARKER = b"SalesCustomerPaymentSummaryBuilder"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if PUB_BE.exists():
        import shutil
        shutil.rmtree(PUB_BE)
    r = subprocess.run(
        ["dotnet", "publish", str(ROOT / "BE_Company" / "BE_Company.csproj"),
         "-c", "Release", "-o", str(PUB_BE), "--nologo"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        return r.returncode
    be = (PUB_BE / "BE_Company.dll").read_bytes()
    print("BE", len(be), hashlib.sha256(be).hexdigest()[:16], MARKER in be)
    if MARKER not in be:
        print("FATAL marker missing")
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_pay_summary_{stamp}.tgz"
    if be_tar.exists():
        be_tar.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB_BE.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_BE)).replace("\\", "/"))

    remote_py = dedent(
        f"""\
        import os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        admins = {ADMINS!r}
        marker = {MARKER!r}

        dest_x = Path(f'/tmp/be_pay_summary_x_{{stamp}}')
        if dest_x.exists():
            shutil.rmtree(dest_x)
        dest_x.mkdir(parents=True)
        with tarfile.open(f'/tmp/be_pay_summary_{{stamp}}.tgz') as t:
            t.extractall(dest_x)
        data = (dest_x / 'BE_Company.dll').read_bytes()
        print('BE_TAR', len(data), marker in data)
        if marker not in data:
            raise SystemExit('marker missing')

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
            for root, dirs, files in os.walk(dest_x):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(dest_x)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('UPDATED', name)

        demo = Path('/opt/qalaa-demo/backend')
        if demo.is_dir():
            for root, dirs, files in os.walk(dest_x):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(dest_x)
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
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_pay_summary_{stamp}.tgz")
        script = f"/tmp/deploy_pay_summary_{stamp}.py"
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
