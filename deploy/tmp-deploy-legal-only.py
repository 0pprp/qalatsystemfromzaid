#!/usr/bin/env python3
"""Deploy already-published BE legal search + FE dist. Preserve appsettings."""
from __future__ import annotations

import os
import tarfile
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")
BE_OUT = ROOT / "deploy" / "tmp-be-company-legal"
FE_DIST = ROOT / "FE_Company" / "dist"
BE_TAR = ROOT / "deploy" / "tmp-be-company-legal.tgz"
FE_TAR = ROOT / "deploy" / "tmp-fe-legal.tgz"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS missing")
        return 2
    if not BE_OUT.is_dir():
        print("BE publish missing")
        return 2
    if not FE_DIST.is_dir():
        print("FE dist missing")
        return 2

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
FE=/opt/qalaa-runtime/AdminFrontend
cp -a "$FE" "/opt/qalaa-runtime/AdminFrontend.bak-legal-$TS" || true
if [ -d "$FE/dist" ]; then
  tar -xzf /tmp/fe-legal.tgz -C "$FE/dist"
elif [ -d "$FE/assets" ] || [ -f "$FE/index.html" ]; then
  tar -xzf /tmp/fe-legal.tgz -C "$FE"
else
  mkdir -p "$FE"
  tar -xzf /tmp/fe-legal.tgz -C "$FE"
fi
chown -R qalaa:qalaa "$FE" || true
echo FE_OK

# Deploy BE to every Admin* with BE_Company.dll
for d in /opt/qalaa-runtime/Admin*; do
  [ -d "$d" ] || continue
  base=$(basename "$d")
  case "$base" in
    AdminFrontend*|Admin*backup*|Admin*-old*|AdminStop|AdminTest|AdminMonthMenu) continue ;;
  esac
  [ -f "$d/BE_Company.dll" ] || { echo "SKIP $base"; continue; }
  unit=""
  for u in $(systemctl list-units --type=service --all --no-legend | awk '{print $1}'); do
    wd=$(systemctl show -p WorkingDirectory --value "$u" 2>/dev/null || true)
    if [ "$wd" = "$d" ]; then unit="$u"; break; fi
  done
  echo "DEPLOY $base -> ${unit:-NONE}"
  [ -n "$unit" ] && systemctl stop "$unit" || true
  cp -a "$d" "${d}.bak-legal-$TS"
  tar -xzf /tmp/be-company-legal.tgz -C "$d"
  chown -R qalaa:qalaa "$d" || true
  [ -n "$unit" ] && systemctl start "$unit" || true
done

# Probe AdminNajaf
NAJAF=/opt/qalaa-runtime/AdminNajaf
unit=""
for u in $(systemctl list-units --type=service --all --no-legend | awk '{print $1}'); do
  wd=$(systemctl show -p WorkingDirectory --value "$u" 2>/dev/null || true)
  if [ "$wd" = "$NAJAF" ]; then unit="$u"; break; fi
done
echo NAJAF_UNIT=$unit
systemctl is-active "$unit" || true
ENVS=$(systemctl show -p Environment --value "$unit" 2>/dev/null || true)
PORT=$(echo "$ENVS" | tr ' ' '\n' | sed -n 's/.*:\([0-9][0-9]*\).*/\1/p' | tail -1)
PORT=${PORT:-5000}
sleep 2
CODE=$(curl -s -o /tmp/legal_probe.body -w '%{http_code}' -X POST "http://127.0.0.1:${PORT}/api/Customers/LegalExcelSearch" -H 'Content-Type: application/json' -d '{"queries":[{"name":"اختبار","phone":"07800000000"}]}')
echo PROBE_HTTP=$CODE PORT=$PORT
head -c 250 /tmp/legal_probe.body; echo
"""
    stdin, stdout, stderr = c.exec_command(remote, timeout=900)
    print(stdout.read().decode("utf-8", "replace"))
    err = stderr.read().decode("utf-8", "replace")
    if err.strip():
        print("STDERR", err[-4000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
