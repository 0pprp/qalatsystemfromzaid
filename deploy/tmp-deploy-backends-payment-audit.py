#!/usr/bin/env python3
"""
Deploy ALL backends + payment/audit SQL migrations.
Does NOT publish/update any Flutter APK / delegate OTA.

Targets:
  - BE_Company → all Admin* (+ demo backend)
  - BE_DelegateWebApplication → all Delegate* (+ demo if present)
  - BE_SalesEmployee → SalesGateway (+ demo gateway)

Migrations (idempotent, all DatabaseCompany* including DEMO):
  - 20261004_CollectionPayment_AtomicPosting_NoLatestId.sql
  - 20261004_CustomersPayments_Create_OutputInserted.sql

Requires env SSH_PASS.
"""
from __future__ import annotations

import hashlib
import json
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
PUB_BE = ROOT / "_publish_be_payment_audit"
PUB_DEL = ROOT / "_publish_delegate_payment_audit"
PUB_GW = ROOT / "_publish_gw_payment_audit"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
MIG_FILES = [
    ROOT / "DatabaseScripts" / "Migrations" / "20261004_CollectionPayment_AtomicPosting_NoLatestId.sql",
    ROOT / "DatabaseScripts" / "Migrations" / "20261004_CustomersPayments_Create_OutputInserted.sql",
]
# ASCII metadata method/type names (UTF-16 literals are harder to probe as raw bytes).
MARKERS = {
    "BE_Company.dll": b"ResolveProvenOfficialSaleId",
    "BE_DelegateWebApplication.dll": b"ClientPaymentId",
    "BE_SalesEmployee.dll": b"AllowedCitiesForMember",
}


def publish(csproj: Path, out: Path, label: str) -> None:
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    print(f"dotnet publish {label}...", flush=True)
    r = subprocess.run(
        ["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(out), "--nologo"],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-3000:])
        print(r.stderr[-3000:])
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
    if not SSH_PASS:
        print("SSH_PASS required (env)")
        return 2

    for m in MIG_FILES:
        if not m.is_file():
            print("missing migration", m)
            return 2

    publish(ROOT / "BE_Company" / "BE_Company.csproj", PUB_BE, "BE_Company")
    publish(
        ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication" / "BE_DelegateWebApplication.csproj",
        PUB_DEL,
        "BE_Delegate",
    )
    publish(ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj", PUB_GW, "BE_SalesEmployee")

    for dll_name, marker in MARKERS.items():
        for pub in (PUB_BE, PUB_DEL, PUB_GW):
            p = pub / dll_name
            if p.is_file():
                data = p.read_bytes()
                ok = marker in data
                print(dll_name, len(data), hashlib.sha256(data).hexdigest()[:16], "marker", ok)
                if not ok:
                    print("FATAL marker missing", dll_name, marker)
                    return 3

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_payment_audit_{stamp}.tgz"
    del_tar = ROOT / f"_del_payment_audit_{stamp}.tgz"
    gw_tar = ROOT / f"_gw_payment_audit_{stamp}.tgz"
    mig_tar = ROOT / f"_mig_payment_audit_{stamp}.tgz"
    make_tar(PUB_BE, be_tar)
    make_tar(PUB_DEL, del_tar)
    make_tar(PUB_GW, gw_tar)
    if mig_tar.exists():
        mig_tar.unlink()
    with tarfile.open(mig_tar, "w:gz") as tar:
        for m in MIG_FILES:
            tar.add(m, arcname=m.name)

    print(
        f"TARS be={be_tar.stat().st_size/1024/1024:.1f}MB "
        f"del={del_tar.stat().st_size/1024/1024:.1f}MB "
        f"gw={gw_tar.stat().st_size/1024/1024:.1f}MB "
        f"mig={mig_tar.stat().st_size/1024:.1f}KB",
        flush=True,
    )

    remote_py = dedent(
        f"""\
        import glob, json, os, shutil, subprocess, tarfile, time
        from pathlib import Path
        stamp = {stamp!r}
        skip = set({sorted(SKIP)!r})
        mig_names = {[m.name for m in MIG_FILES]!r}

        def extract(tgz, dest):
            dest = Path(dest)
            shutil.rmtree(dest, ignore_errors=True)
            dest.mkdir(parents=True)
            with tarfile.open(tgz) as t:
                t.extractall(dest)
            return dest

        def copy_tree(src: Path, dest: Path):
            for root, _dirs, files in os.walk(src):
                for f in files:
                    if f in skip:
                        continue
                    full = Path(root) / f
                    rel = full.relative_to(src)
                    target = dest / rel
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(full, target)

        def units_for_dirs(dirs):
            out = subprocess.getoutput('systemctl list-units --type=service --all --no-legend')
            wd_to_unit = {{}}
            for line in out.splitlines():
                u = line.split()[0] if line.strip() else ''
                if not u.endswith('.service'):
                    continue
                wd = subprocess.getoutput(f'systemctl show {{u}} -p WorkingDirectory --value').strip()
                if wd:
                    wd_to_unit[wd] = u
            units = []
            for d in dirs:
                u = wd_to_unit.get(str(d))
                if u and u not in units:
                    units.append(u)
            return units

        be_x = extract(f'/tmp/be_payment_audit_{{stamp}}.tgz', f'/tmp/be_payment_audit_x_{{stamp}}')
        del_x = extract(f'/tmp/del_payment_audit_{{stamp}}.tgz', f'/tmp/del_payment_audit_x_{{stamp}}')
        gw_x = extract(f'/tmp/gw_payment_audit_{{stamp}}.tgz', f'/tmp/gw_payment_audit_x_{{stamp}}')
        mig_x = extract(f'/tmp/mig_payment_audit_{{stamp}}.tgz', f'/tmp/mig_payment_audit_x_{{stamp}}')

        be_targets = []
        for d in sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
            base = os.path.basename(d).lower()
            if 'backup' in base or base.endswith('-old') or 'frontend' in base:
                continue
            if os.path.isfile(os.path.join(d, 'BE_Company.dll')):
                be_targets.append(Path(d))
        demo_be = Path('/opt/qalaa-demo/backend')
        if demo_be.is_dir() and (demo_be / 'BE_Company.dll').is_file():
            be_targets.append(demo_be)
        print('BE_TARGETS', len(be_targets))

        del_targets = []
        for d in sorted(glob.glob('/opt/qalaa-runtime/Delegate*')):
            base = os.path.basename(d).lower()
            if 'backup' in base or base.endswith('.bak'):
                continue
            if os.path.isfile(os.path.join(d, 'BE_DelegateWebApplication.dll')):
                del_targets.append(Path(d))
        for d in sorted(glob.glob('/opt/qalaa-demo/**/BE_DelegateWebApplication.dll', recursive=True)):
            del_targets.append(Path(d).parent)
        # unique
        seen = set()
        uniq = []
        for d in del_targets:
            s = str(d)
            if s not in seen:
                seen.add(s)
                uniq.append(d)
        del_targets = uniq
        print('DEL_TARGETS', len(del_targets))

        gw_targets = []
        for d in ['/opt/qalaa-runtime/SalesGateway', '/opt/qalaa-demo/sales-gateway', '/opt/qalaa-runtime/BE_SalesEmployee']:
            p = Path(d)
            if p.is_dir() and (p / 'BE_SalesEmployee.dll').is_file():
                gw_targets.append(p)
        print('GW_TARGETS', len(gw_targets))

        all_dirs = be_targets + del_targets + gw_targets
        units = units_for_dirs(all_dirs)
        print('STOP', len(units))
        for u in units:
            subprocess.run(['systemctl', 'stop', u], check=False)
        time.sleep(2)

        for dest in be_targets:
            copy_tree(be_x, dest)
            print('UPDATED_BE', dest.name if dest.name else dest)
        for dest in del_targets:
            copy_tree(del_x, dest)
            print('UPDATED_DEL', dest.name if dest.name else dest)
        for dest in gw_targets:
            copy_tree(gw_x, dest)
            print('UPDATED_GW', dest)

        print('START', len(units))
        for u in units:
            subprocess.run(['systemctl', 'start', u], check=False)
            print('ACTIVE', u, subprocess.getoutput(f'systemctl is-active {{u}}'))
        # ensure known gateway units
        for u in ['qalaa-sales-gateway', 'qalaa-demo-sales-gateway', 'qalaa-demo-backend']:
            subprocess.run(['systemctl', 'restart', u], check=False)

        # Apply migrations across catalogs discovered from Admin*/Delegate* connection strings
        catalogs = {{}}
        for cfg_path in glob.glob('/opt/qalaa-runtime/Admin*/appsettings.json') + glob.glob('/opt/qalaa-runtime/Delegate*/appsettings.json') + [
            '/opt/qalaa-demo/backend/appsettings.json'
        ]:
            try:
                cfg = json.load(open(cfg_path, encoding='utf-8'))
                cs = (cfg.get('ConnectionStrings') or {{}}).get('DataBaseConnection') or ''
            except Exception as e:
                print('CFG_ERR', cfg_path, e)
                continue
            parts = {{}}
            for p in cs.split(';'):
                if '=' in p:
                    k, v = p.split('=', 1)
                    parts[k.strip().lower()] = v.strip()
            db = parts.get('initial catalog') or parts.get('database')
            if not db or not db.lower().startswith('databasecompany'):
                continue
            catalogs[db] = {{
                'server': parts.get('server') or parts.get('data source'),
                'user': parts.get('user id') or parts.get('uid'),
                'pwd': parts.get('password') or parts.get('pwd'),
                'db': db,
            }}
        print('DBS', len(catalogs), sorted(catalogs.keys()))

        sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'
        if not os.path.isfile(sqlcmd):
            sqlcmd = 'sqlcmd'
        mig_ok = 0
        mig_fail = 0
        for db, cinfo in sorted(catalogs.items()):
            for name in mig_names:
                sql_path = mig_x / name
                cmd = [
                    sqlcmd, '-C', '-I',
                    '-S', cinfo['server'],
                    '-d', cinfo['db'],
                    '-U', cinfo['user'],
                    '-P', cinfo['pwd'],
                    '-i', str(sql_path),
                    '-b', '-W',
                ]
                r = subprocess.run(cmd, capture_output=True, text=True)
                if r.returncode == 0:
                    mig_ok += 1
                    print('MIG_OK', db, name)
                else:
                    mig_fail += 1
                    print('MIG_FAIL', db, name, 'rc', r.returncode)
                    print((r.stdout or '')[-500:])
                    print((r.stderr or '')[-500:])
        print('MIG_SUMMARY ok', mig_ok, 'fail', mig_fail)

        # Verify markers on one BE + one Delegate
        if be_targets:
            data = (be_targets[0] / 'BE_Company.dll').read_bytes()
            print('VERIFY_BE', be_targets[0], b'ResolveProvenOfficialSaleId' in data)
        if del_targets:
            data = (del_targets[0] / 'BE_DelegateWebApplication.dll').read_bytes()
            print('VERIFY_DEL', del_targets[0], b'ClientPaymentId' in data)
        print('DONE_NO_DELEGATE_APK')
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(be_tar), f"/tmp/be_payment_audit_{stamp}.tgz")
        sftp.put(str(del_tar), f"/tmp/del_payment_audit_{stamp}.tgz")
        sftp.put(str(gw_tar), f"/tmp/gw_payment_audit_{stamp}.tgz")
        sftp.put(str(mig_tar), f"/tmp/mig_payment_audit_{stamp}.tgz")
        with sftp.file(f"/tmp/deploy_payment_audit_{stamp}.py", "w") as f:
            f.write(remote_py)
        sftp.close()

        _, stdout, stderr = c.exec_command(
            f"python3 /tmp/deploy_payment_audit_{stamp}.py", timeout=1800
        )
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[-2000:])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
