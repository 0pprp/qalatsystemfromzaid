#!/usr/bin/env python3
"""Stop Najaf, clean-install publish (keep appsettings), start, probe employee trend."""
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


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    dll = (PUB / "BE_Company.dll").read_bytes()
    print("local", len(dll), hashlib.sha256(dll).hexdigest()[:16], b"PeriodAggregate" in dll)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_be_najaf_fix_{stamp}.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in PUB.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB)).replace("\\", "/"))

    remote_py = dedent(
        f"""\
        import hashlib, os, shutil, subprocess, tarfile, time, urllib.error, urllib.request
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({SKIP!r})
        extract = Path('/tmp/be_najaf_fix_' + stamp)
        if extract.exists():
            shutil.rmtree(extract)
        extract.mkdir()
        with tarfile.open('/tmp/be_najaf_fix_' + stamp + '.tgz') as t:
            t.extractall(extract)
        data = (extract / 'BE_Company.dll').read_bytes()
        print('tar', len(data), hashlib.sha256(data).hexdigest()[:16], b'PeriodAggregate' in data)
        subprocess.run(['systemctl', 'stop', 'qalaa-company-sharenewnajaf'], check=False)
        time.sleep(2)
        dest = Path('/opt/qalaa-runtime/AdminNajaf')
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
                    print('skip_del', p.name, e)
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
        print(
            'installed',
            len(installed),
            hashlib.sha256(installed).hexdigest()[:16],
            b'PeriodAggregate' in installed,
            b'EmployeeTrendAsync' in installed,
        )
        subprocess.run(['systemctl', 'start', 'qalaa-company-sharenewnajaf'], check=False)
        time.sleep(5)
        print('active', subprocess.getoutput('systemctl is-active qalaa-company-sharenewnajaf'))
        key = ''
        conf = Path('/etc/systemd/system/qalaa-company-sharenewnajaf.service.d/sales-gateway.conf')
        if conf.is_file():
            for line in conf.read_text().splitlines():
                if 'GatewayKey=' in line:
                    key = line.split('GatewayKey=', 1)[1].strip().strip('"')
        print('keylen', len(key))

        def hit(scope, eid=7):
            url = (
                'http://127.0.0.1:5301/api/sales-manager/stats/trend'
                f'?scope={{scope}}&id={{eid}}&fromDate=2026-09-17&toDate=2026-09-23'
            )
            req = urllib.request.Request(
                url,
                headers={{
                    'X-Sales-Gateway-Key': key,
                    'X-Sales-Manager-Name': 'director-probe',
                }},
            )
            try:
                with urllib.request.urlopen(req, timeout=90) as resp:
                    body = resp.read().decode('utf-8', 'replace')
                    print(scope, 'HTTP', resp.status, body[:260].replace(chr(10), ' '))
            except urllib.error.HTTPError as e:
                print(scope, 'HTTP', e.code, e.read()[:400])
            except Exception as e:
                print(scope, 'ERR', repr(e))

        hit('employee', 7)
        hit('branch', 0)
        print(subprocess.getoutput('journalctl -u qalaa-company-sharenewnajaf -n 40 --no-pager | tail -30'))
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(tar_path), f"/tmp/be_najaf_fix_{stamp}.tgz")
        script = f"/tmp/fix_najaf_{stamp}.py"
        with sftp.file(script, "w") as f:
            f.write(remote_py)
        sftp.close()
        _, out, err = c.exec_command(f"python3 {script}", timeout=240)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[-2000:])
        return out.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
