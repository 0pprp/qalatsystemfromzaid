#!/usr/bin/env python3
"""Clean-install BE_Company publish on all Admin* (stop → wipe dlls → copy → start)."""
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
SSH_PASS = os.environ["SSH_PASS"]
ROOT = Path(__file__).resolve().parent.parent
PUB = ROOT / "_publish_be_rating_fix"
SKIP = [
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
]

ADMIN_SERVICE = {
    "AdminBaghdadKarkh": "qalaa-company-sharenewrkarkok",  # verify mapping below from server if needed
}

# Prefer discovering units by WorkingDirectory on the server.
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    dll = (PUB / "BE_Company.dll").read_bytes()
    assert b"PeriodAggregate" in dll
    print("local", len(dll), hashlib.sha256(dll).hexdigest()[:16])

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_be_clean_all_{stamp}.tgz"
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
        admins = {ADMINS!r}
        extract = Path('/tmp/be_clean_all_' + stamp)
        if extract.exists():
            shutil.rmtree(extract)
        extract.mkdir()
        with tarfile.open('/tmp/be_clean_all_' + stamp + '.tgz') as t:
            t.extractall(extract)
        data = (extract / 'BE_Company.dll').read_bytes()
        print('tar', len(data), hashlib.sha256(data).hexdigest()[:16], b'PeriodAggregate' in data)

        # map WorkingDirectory -> unit
        units = subprocess.getoutput("systemctl list-units --type=service --all --no-legend").splitlines()
        wd_to_unit = {{}}
        for line in units:
            u = line.split()[0] if line.strip() else ''
            if not u.endswith('.service'):
                continue
            if 'qalaa-company' not in u and 'demo' not in u:
                continue
            wd = subprocess.getoutput(f'systemctl show {{u}} -p WorkingDirectory --value').strip()
            if wd:
                wd_to_unit[wd] = u

        updated = 0
        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            if not dest.is_dir():
                continue
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
            for root, dirs, files in os.walk(extract):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(extract)
                    dst = dest / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            installed = (dest / 'BE_Company.dll').read_bytes()
            ok = b'PeriodAggregate' in installed and len(installed) == len(data)
            print('UPDATED', name, 'ok', ok, 'size', len(installed))
            updated += 1

        # demo backend
        demo = Path('/opt/qalaa-demo/backend')
        if demo.is_dir():
            for root, dirs, files in os.walk(extract):
                for f in files:
                    if f in skip:
                        continue
                    src = Path(root) / f
                    rel = src.relative_to(extract)
                    dst = demo / rel
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dst)
            print('UPDATED_DEMO')
            subprocess.run(['systemctl', 'restart', 'qalaa-demo-backend.service'], check=False)

        for name in admins:
            dest = Path('/opt/qalaa-runtime') / name
            unit = wd_to_unit.get(str(dest), '')
            if unit:
                subprocess.run(['systemctl', 'start', unit], check=False)
                print('STARTED', unit)

        time.sleep(3)
        najaf = Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll').read_bytes()
        print('NAJAF', len(najaf), hashlib.sha256(najaf).hexdigest()[:16], b'PeriodAggregate' in najaf)
        print('DONE updated', updated)
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/be_clean_all_{stamp}.tgz")
        script = f"/tmp/clean_all_{stamp}.py"
        with sftp.file(script, "w") as f:
            f.write(remote_py)
        sftp.close()
        _, out, err = c.exec_command(f"python3 {script}", timeout=600)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[-2000:])
        code = out.channel.recv_exit_status()
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
