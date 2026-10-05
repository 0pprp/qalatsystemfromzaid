#!/usr/bin/env python3
"""Deploy BE_Company with BM stats/rating-customers to all Admin* + demo."""
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
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUB_BE = ROOT / "_publish_be_bm_rating"
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
MARKER = "التقييم حتى اليوم".encode("utf-16le")
MARKER_ASCII = b"GetRatingCustomersAsync"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    dll_path = PUB_BE / "BE_Company.dll"
    if not dll_path.exists():
        print("missing publish", PUB_BE)
        return 2
    be = dll_path.read_bytes()
    mark_ok = MARKER in be or MARKER_ASCII in be
    print("BE", len(be), hashlib.sha256(be).hexdigest()[:16], mark_ok)
    if not mark_ok:
        print("FATAL: rating-through-today marker missing in BE_Company.dll")
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_bm_rating_{stamp}.tgz"
    if be_tar.exists():
        be_tar.unlink()
    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB_BE.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_BE)).replace("\\", "/"))

    remote_py = dedent(
        f"""\
        import hashlib, os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        admins = {ADMINS!r}
        marker = {MARKER!r}

        extract = Path('/tmp/be_bm_rating_x_' + stamp)
        if extract.exists():
            shutil.rmtree(extract)
        extract.mkdir()
        with tarfile.open('/tmp/be_bm_rating_' + stamp + '.tgz') as t:
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
        for name in admins:
            d = Path('/opt/qalaa-runtime') / name
            if (d / 'BE_Company.dll').is_file():
                targets.append(d)
        for d in Path('/opt/qalaa-runtime').glob('Admin*'):
            if (d / 'BE_Company.dll').is_file() and d not in targets:
                if 'backup' in d.name.lower() or d.name.endswith('-old'):
                    continue
                targets.append(d)
        # demo-backend if present
        for d in Path('/opt/qalaa-runtime').glob('*demo*'):
            if (d / 'BE_Company.dll').is_file() and d not in targets:
                targets.append(d)

        print('TARGETS', len(targets))
        ok = 0
        for d in targets:
            unit = wd_to_unit.get(str(d), '')
            if unit:
                subprocess.run(['systemctl', 'stop', unit], check=False)
                time.sleep(0.4)
            for src in extract.rglob('*'):
                if not src.is_file() or src.name in skip:
                    continue
                rel = src.relative_to(extract)
                dest = d / rel
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dest)
            live = (d / 'BE_Company.dll').read_bytes()
            mark_ok = marker in live
            print(d.name, 'marker', mark_ok, 'unit', unit or '-')
            if unit:
                subprocess.run(['systemctl', 'start', unit], check=False)
                time.sleep(0.3)
            if mark_ok:
                ok += 1
        print('OK', ok, '/', len(targets))
        print('DONE')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        remote_tgz = f"/tmp/be_bm_rating_{stamp}.tgz"
        remote_py_path = f"/tmp/deploy_be_bm_rating_{stamp}.py"
        sftp = c.open_sftp()
        print("upload tgz...", flush=True)
        sftp.put(str(be_tar), remote_tgz)
        with sftp.file(remote_py_path, "w") as f:
            f.write(remote_py)
        sftp.close()
        stdin, stdout, stderr = c.exec_command(f"python3 {remote_py_path}", timeout=600)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:800])
        code = stdout.channel.recv_exit_status()
        print("EXIT", code)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
