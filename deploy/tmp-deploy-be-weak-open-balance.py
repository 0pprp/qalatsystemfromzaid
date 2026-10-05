#!/usr/bin/env python3
"""Deploy BE_Company WeakWeek open-balance filter to all Admin*."""
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
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PROJ = ROOT / "BE_Company" / "BE_Company.csproj"
PUB = ROOT / "_publish_be_weak_open"
SKIP = [
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
]
MARKER = "RatingNoInventDraftTotals".encode("utf-16le")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUB.exists():
        shutil.rmtree(PUB)
    PUB.mkdir(parents=True)
    print("dotnet publish...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(PROJ), "-c", "Release", "-o", str(PUB), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-2500:])
        print(r.stderr[-2500:])
        return r.returncode

    dll = (PUB / "BE_Company.dll").read_bytes()
    print("BE", len(dll), hashlib.sha256(dll).hexdigest()[:16], MARKER in dll)
    if MARKER not in dll:
        print("FATAL: marker missing")
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_be_weak_open_{stamp}.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))

    remote_py = dedent(
        f"""\
        import hashlib, os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        marker = {MARKER!r}
        extract = Path('/tmp/be_weak_open_x_' + stamp)
        if extract.exists():
            shutil.rmtree(extract)
        extract.mkdir()
        with tarfile.open('/tmp/be_weak_open_' + stamp + '.tgz') as t:
            t.extractall(extract)
        data = (extract / 'BE_Company.dll').read_bytes()
        print('TAR', len(data), hashlib.sha256(data).hexdigest()[:16], marker in data)
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

        targets = []
        for d in Path('/opt/qalaa-runtime').glob('Admin*'):
            if (d / 'BE_Company.dll').is_file():
                if 'backup' in d.name.lower() or d.name.endswith('-old'):
                    continue
                targets.append(d)
        for d in Path('/opt/qalaa-runtime').glob('*demo*'):
            if (d / 'BE_Company.dll').is_file() and d not in targets:
                targets.append(d)

        print('TARGETS', len(targets))
        ok = 0
        for d in targets:
            unit = wd_to_unit.get(str(d), '')
            if unit:
                subprocess.run(['systemctl', 'stop', unit], check=False)
                time.sleep(0.35)
            for src in extract.rglob('*'):
                if not src.is_file() or src.name in skip:
                    continue
                dest = d / src.relative_to(extract)
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dest)
            live_ok = marker in (d / 'BE_Company.dll').read_bytes()
            print(d.name, 'marker', live_ok, 'unit', unit or '-')
            if unit:
                subprocess.run(['systemctl', 'start', unit], check=False)
                time.sleep(0.25)
            if live_ok:
                ok += 1
        print('OK', ok, '/', len(targets))
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/be_weak_open_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_be_weak_open_{stamp}.py", "w") as f:
            f.write(remote_py)
        sftp.close()
        _, stdout, stderr = c.exec_command(
            f"python3 /tmp/deploy_be_weak_open_{stamp}.py", timeout=600
        )
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
