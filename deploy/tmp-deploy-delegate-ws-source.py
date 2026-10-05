#!/usr/bin/env python3
"""Deploy BE_Delegate (WrongSale ResolveCityValue) + patch najaf SalesGateway env."""
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
PROJ = (
    ROOT
    / "BE_DelegateWebApplication"
    / "BE_DelegateWebApplication"
    / "BE_DelegateWebApplication.csproj"
)
PUBLISH = ROOT / "_publish_delegate_ws_source"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    if PUBLISH.exists():
        shutil.rmtree(PUBLISH)
    PUBLISH.mkdir(parents=True)
    print("dotnet publish delegate...", flush=True)
    r = subprocess.run(
        [
            "dotnet",
            "publish",
            str(PROJ),
            "-c",
            "Release",
            "-o",
            str(PUBLISH),
            "--nologo",
        ],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-2500:], r.stderr[-2500:])
        return r.returncode

    stamp = time.strftime("%Y%m%d_%H%M%S")
    local_tar = ROOT / f"_publish_delegate_ws_source_{stamp}.tar.gz"
    if local_tar.exists():
        local_tar.unlink()
    with tarfile.open(local_tar, "w:gz") as tar:
        for p in PUBLISH.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUBLISH)).replace("\\", "/"))
    print(f"tar {local_tar.stat().st_size / 1024 / 1024:.1f}MB", flush=True)

    remote_tar = f"/opt/qalaa-updates/delegate-ws-source-{stamp}.tar.gz"
    apply_path = f"/tmp/apply_delegate_ws_{stamp}.py"
    apply = f"""
import os, shutil, subprocess, tarfile
from pathlib import Path

stamp = "{stamp}"
remote_tar = "{remote_tar}"
extract = f"/tmp/delegate_ws_source_{{stamp}}"
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
with tarfile.open(remote_tar, "r:gz") as t:
    t.extractall(extract)

skip = {{
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}}
dirs = []
for name in sorted(os.listdir("/opt/qalaa-runtime")):
    low = name.lower()
    if not low.startswith("delegate") or "backup" in low or ".bak" in low:
        continue
    d = os.path.join("/opt/qalaa-runtime", name)
    if os.path.isfile(os.path.join(d, "BE_DelegateWebApplication.dll")):
        dirs.append(d)
if os.path.isfile("/opt/qalaa-demo/follower-backend/BE_DelegateWebApplication.dll"):
    dirs.append("/opt/qalaa-demo/follower-backend")
print("DIRS", len(dirs))

for dest in dirs:
    for walk_root, _, files in os.walk(extract):
        for f in files:
            if f in skip:
                continue
            full = os.path.join(walk_root, f)
            rel = os.path.relpath(full, extract)
            target = os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print("UPDATED", os.path.basename(dest))

units = [
    u.split()[0].removesuffix(".service")
    for u in subprocess.getoutput(
        "systemctl list-units --type=service --all --no-legend 'qalaa-delegate*'"
    ).splitlines()
    if u.strip()
]
for u in units + ["qalaa-demo-follower"]:
    if subprocess.run(["systemctl", "cat", u], capture_output=True).returncode == 0:
        subprocess.run(["systemctl", "restart", u], check=False)
        print("restarted", u, subprocess.getoutput(f"systemctl is-active {{u}}"))

env_path = Path("/etc/qalaa-production/delegate-najaf-sales-gateway.env")
existing = {{}}
if env_path.exists():
    for line in env_path.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.strip().startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        existing[k.strip()] = v
existing["SalesGateway__BaseUrl"] = "http://127.0.0.1:5280/api/"
existing["Branch__CityValue"] = "DatabaseCompanyNajaf"
existing["Branch__CityName"] = "النجف"
env_path.write_text(
    "\\n".join(f"{{k}}={{v}}" for k, v in existing.items()) + "\\n",
    encoding="utf-8",
)
print("ENV_KEYS", sorted(existing.keys()))
for k in ("SalesGateway__BaseUrl", "Branch__CityValue", "Branch__CityName"):
    print("ENV", k, existing.get(k))

subprocess.run(["systemctl", "restart", "qalaa-delegatehaidernajaflast"], check=False)
print(
    "restarted najaf",
    subprocess.getoutput("systemctl is-active qalaa-delegatehaidernajaflast"),
)

dll = "/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll"
data = Path(dll).read_bytes()
print("DLL has ResolveCityValue", b"ResolveCityValue" in data)
print("DONE")
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    try:
        c.exec_command("mkdir -p /opt/qalaa-updates")[1].channel.recv_exit_status()
        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        with sftp.file(apply_path, "w") as rf:
            rf.write(apply)
        sftp.close()

        _, stdout, stderr = c.exec_command(f"python3 {apply_path}", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:2000], file=sys.stderr)
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
