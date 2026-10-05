#!/usr/bin/env python3
"""Publish+deploy BE_Company (all Admin*) + FE AdminFrontend for legal search."""
from __future__ import annotations

import os
import subprocess
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")
BE_OUT = ROOT / "deploy" / "tmp-be-company-legal"
FE_DIST = ROOT / "FE_Company" / "dist"
BE_TAR = ROOT / "deploy" / "tmp-be-company-legal.tgz"
FE_TAR = ROOT / "deploy" / "tmp-fe-legal.tgz"


def run(cmd: list[str], cwd: Path | None = None) -> None:
    print("+", " ".join(cmd))
    subprocess.check_call(cmd, cwd=str(cwd or ROOT))


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS missing")
        return 2

    run(
        [
            "dotnet",
            "publish",
            "BE_Company\\BE_Company.csproj",
            "-c",
            "Release",
            "-o",
            str(BE_OUT),
            "--nologo",
        ]
    )
    run(["npm", "run", "build"], cwd=ROOT / "FE_Company")

    with tarfile.open(BE_TAR, "w:gz") as t:
        for p in BE_OUT.rglob("*"):
            if not p.is_file():
                continue
            name = p.name.lower()
            if name.startswith("appsettings") and name.endswith(".json"):
                continue
            t.add(p, arcname=p.relative_to(BE_OUT).as_posix())
    print("be_tar", BE_TAR.stat().st_size)

    with tarfile.open(FE_TAR, "w:gz") as t:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                t.add(p, arcname=p.relative_to(FE_DIST).as_posix())
    print("fe_tar", FE_TAR.stat().st_size)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=40,
        allow_agent=False,
        look_for_keys=False,
    )
    sftp = c.open_sftp()
    sftp.put(str(BE_TAR), "/tmp/be-company-legal.tgz")
    sftp.put(str(FE_TAR), "/tmp/fe-legal.tgz")
    sftp.close()

    remote = r"""
set -e
TS=$(date +%Y%m%d_%H%M%S)
# FE
FE=/opt/qalaa-runtime/AdminFrontend
cp -a "$FE" "/opt/qalaa-runtime/AdminFrontend.bak-$TS" || true
# keep assets under dist or root depending on layout
if [ -d "$FE/dist" ]; then
  tar -xzf /tmp/fe-legal.tgz -C "$FE/dist"
else
  tar -xzf /tmp/fe-legal.tgz -C "$FE"
fi
chown -R qalaa:qalaa "$FE" || true

# BE all Admin* (company branch APIs) — stop, extract, start
mapfile -t DIRS < <(ls -d /opt/qalaa-runtime/Admin* 2>/dev/null | grep -v AdminFrontend | grep -v '\-old' | grep -v backup || true)
echo "ADMIN_DIRS=${#DIRS[@]}"
for d in "${DIRS[@]}"; do
  base=$(basename "$d")
  # skip non-BE folders
  if [ ! -f "$d/BE_Company.dll" ]; then
    echo "SKIP $base"
    continue
  fi
  svc="qalaa-company-${base#Admin}"
  # actual service names are like qalaa-company-share* — discover by WorkingDirectory
  unit=$(systemctl list-units --type=service --all --no-legend | awk '{print $1}' | while read u; do
    wd=$(systemctl show -p WorkingDirectory --value "$u" 2>/dev/null || true)
    [ "$wd" = "$d" ] && echo "$u" && break
  done)
  echo "DEPLOY $base unit=${unit:-none}"
  if [ -n "$unit" ]; then systemctl stop "$unit" || true; fi
  cp -a "$d" "${d}.bak-$TS" || true
  tar -xzf /tmp/be-company-legal.tgz -C "$d"
  chown -R qalaa:qalaa "$d" || true
  if [ -n "$unit" ]; then systemctl start "$unit" || true; fi
done

# smoke: find najaf service port and probe route
NAJAF=$(ls -d /opt/qalaa-runtime/AdminNajaf 2>/dev/null | head -1)
unit=$(systemctl list-units --type=service --all --no-legend | awk '{print $1}' | while read u; do
  wd=$(systemctl show -p WorkingDirectory --value "$u" 2>/dev/null || true)
  [ "$wd" = "$NAJAF" ] && echo "$u" && break
done)
echo NAJAF_UNIT=$unit
URLS=$(systemctl show -p Environment --value "$unit" 2>/dev/null | tr ' ' '\n' | grep ASPNETCORE_URLS || true)
echo URLS=$URLS
PORT=$(echo "$URLS" | grep -oE '[0-9]+' | tail -1)
PORT=${PORT:-8080}
sleep 2
CODE=$(curl -s -o /tmp/legal_probe.body -w '%{http_code}' -X POST "http://127.0.0.1:${PORT}/api/Customers/LegalExcelSearch" -H 'Content-Type: application/json' -d '{"queries":[{"name":"اختبار","phone":"07800000000"}]}')
echo PROBE_HTTP=$CODE
head -c 200 /tmp/legal_probe.body; echo
"""
    stdin, stdout, stderr = c.exec_command(remote, timeout=600)
    print(stdout.read().decode("utf-8", "replace"))
    err = stderr.read().decode("utf-8", "replace")
    if err.strip():
        print("STDERR", err[-3000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
