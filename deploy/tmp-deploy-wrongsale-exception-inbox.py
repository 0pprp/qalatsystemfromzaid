#!/usr/bin/env python3
"""Deploy WrongSale exception inbox enrichment: BE_SalesEmployee + BE_Company + BE_Delegate. No Flutter OTA."""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUB_GW = ROOT / "_publish_wrongsale_gw"
PUB_BE = ROOT / "_publish_wrongsale_be"
PUB_DEL = ROOT / "_publish_wrongsale_del"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
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
        print(r.stdout[-2500:], r.stderr[-2500:])
        raise SystemExit(r.returncode)


def make_tar(src: Path, tar_path: Path) -> None:
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in src.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(src)).replace("\\", "/"))


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    publish(ROOT / "BE_SalesEmployee" / "BE_SalesEmployee.csproj", PUB_GW, "gateway")
    publish(ROOT / "BE_Company" / "BE_Company.csproj", PUB_BE, "company")
    publish(
        ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication" / "BE_DelegateWebApplication.csproj",
        PUB_DEL,
        "delegate",
    )

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tars = {
        "gw": (PUB_GW, ROOT / f"_publish_wrongsale_gw_{stamp}.tar.gz"),
        "be": (PUB_BE, ROOT / f"_publish_wrongsale_be_{stamp}.tar.gz"),
        "del": (PUB_DEL, ROOT / f"_publish_wrongsale_del_{stamp}.tar.gz"),
    }
    for _, (src, path) in tars.items():
        make_tar(src, path)
        print(f"tar {path.name} {path.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    remote = {}
    for k, (_, path) in tars.items():
        rp = f"/opt/qalaa-updates/wrongsale-{k}-{stamp}.tar.gz"
        sftp.put(str(path), rp)
        remote[k] = rp
    sftp.close()

    script = f"""
import os, shutil, subprocess, tarfile
stamp='{stamp}'
skip={{'appsettings.json','appsettings.Development.json','appsettings.Production.json'}}

def apply(remote_tar, dirs, units):
    extract='/tmp/wrongsale_' + os.path.basename(remote_tar).replace('.tar.gz','')
    shutil.rmtree(extract, ignore_errors=True)
    os.makedirs(extract, exist_ok=True)
    tarfile.open(remote_tar).extractall(extract)
    print('DIRS', len(dirs), remote_tar)
    for dest in dirs:
        for root,_,files in os.walk(extract):
            for f in files:
                if f in skip: continue
                full=os.path.join(root,f)
                rel=os.path.relpath(full, extract)
                target=os.path.join(dest, rel)
                os.makedirs(os.path.dirname(target), exist_ok=True)
                shutil.copy2(full, target)
        print('UPDATED', os.path.basename(dest))
    for u in units:
        if subprocess.run(['systemctl','cat',u], capture_output=True).returncode==0:
            subprocess.run(['systemctl','restart',u], check=False)
            print('restarted', u)

gw_dirs=[d for d in ['/opt/qalaa-runtime/SalesGateway','/opt/qalaa-demo/sales-gateway','/opt/qalaa-runtime/BE_SalesEmployee'] if os.path.isfile(os.path.join(d,'BE_SalesEmployee.dll'))]
apply('{remote["gw"]}', gw_dirs, ['qalaa-sales-gateway','qalaa-demo-sales-gateway'])

be_dirs=[]
for name in sorted(os.listdir('/opt/qalaa-runtime')):
    if not name.lower().startswith('admin') or 'backup' in name.lower(): continue
    d=os.path.join('/opt/qalaa-runtime', name)
    if os.path.isfile(os.path.join(d,'BE_Company.dll')): be_dirs.append(d)
if os.path.isfile('/opt/qalaa-demo/backend/BE_Company.dll'): be_dirs.append('/opt/qalaa-demo/backend')
be_units=[u.split()[0].removesuffix('.service') for u in subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-company*'").splitlines() if u.strip()]
apply('{remote["be"]}', be_dirs, be_units + ['qalaa-demo-backend'])

del_dirs=[]
for name in sorted(os.listdir('/opt/qalaa-runtime')):
    if not name.lower().startswith('delegate') or 'backup' in name.lower(): continue
    d=os.path.join('/opt/qalaa-runtime', name)
    if os.path.isfile(os.path.join(d,'BE_DelegateWebApplication.dll')): del_dirs.append(d)
if os.path.isfile('/opt/qalaa-demo/follower-backend/BE_DelegateWebApplication.dll'):
    del_dirs.append('/opt/qalaa-demo/follower-backend')
del_units=[u.split()[0].removesuffix('.service') for u in subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-delegate*'").splitlines() if u.strip()]
apply('{remote["del"]}', del_dirs, del_units + ['qalaa-demo-follower'])
print('DONE')
"""
    _, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{script}\nPY")
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    code = stdout.channel.recv_exit_status()
    print(out.encode("ascii", errors="replace").decode("ascii"))
    if err.strip():
        print(err.encode("ascii", errors="replace").decode("ascii"), file=sys.stderr)
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
