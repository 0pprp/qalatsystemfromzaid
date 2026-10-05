#!/usr/bin/env python3
"""Restore missing KYC/shop images from backups into live Admin* App_Data (ignore-existing)."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = r'''
set -euo pipefail

echo "=== before Najaf counts ==="
echo -n "live customer-docs: "; ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs 2>/dev/null | wc -l
echo -n "backup customer-docs: "; ls /opt/qalaa-migration/backups/20260912_190813/AdminNajaf/App_Data/sales/customer-docs 2>/dev/null | wc -l
echo -n "live sale dirs: "; ls -d /opt/qalaa-runtime/AdminNajaf/App_Data/sales/*/ 2>/dev/null | wc -l

# Restore Najaf from migration backup (only missing files)
if [ -d /opt/qalaa-migration/backups/20260912_190813/AdminNajaf/App_Data/sales ]; then
  mkdir -p /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs
  rsync -a --ignore-existing \
    /opt/qalaa-migration/backups/20260912_190813/AdminNajaf/App_Data/sales/ \
    /opt/qalaa-runtime/AdminNajaf/App_Data/sales/
  echo RESTORED_NAJAF_FROM_MIGRATION_BACKUP
fi

# For every live Admin*, restore missing App_Data/sales from newest matching backup that has customer-docs
for dest in /opt/qalaa-runtime/Admin*; do
  [ -d "$dest" ] || continue
  base=$(basename "$dest")
  case "$base" in *backup*|*old*|*.bak*) continue;; esac
  [ -d "$dest/App_Data/sales" ] || mkdir -p "$dest/App_Data/sales/customer-docs"

  # Candidate backups: migration snapshot, pre-return, bak folders with same basename prefix
  cands=""
  for c in \
    "/opt/qalaa-migration/backups/20260912_190813/$base/App_Data/sales" \
    $(ls -d /opt/qalaa-backups/${base}-pre-return-* /opt/qalaa-backups/${base}-pre-bulk-* /opt/qalaa-runtime/${base}.bak-* 2>/dev/null | sort -r); do
    if [ -d "$c/App_Data/sales" ]; then
      cands="$cands $c/App_Data/sales"
    elif [ -d "$c" ] && [ "$(basename "$(dirname "$c")")" = "sales" -o -d "$c/customer-docs" ]; then
      # already sales path
      cands="$cands $c"
    fi
  done

  # Also search runtime bak folders
  for c in /opt/qalaa-runtime/${base}.bak-*/App_Data/sales; do
    [ -d "$c" ] && cands="$cands $c"
  done

  for src in $cands; do
    [ -d "$src" ] || continue
    before=$(ls "$dest/App_Data/sales/customer-docs" 2>/dev/null | wc -l)
    rsync -a --ignore-existing "$src"/ "$dest/App_Data/sales/"
    after=$(ls "$dest/App_Data/sales/customer-docs" 2>/dev/null | wc -l)
    echo "SYNC $base from $src docs $before -> $after"
  done
done

echo "=== after Najaf ==="
echo -n "live customer-docs: "; ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs 2>/dev/null | wc -l
echo -n "has 2.jpg: "; ls -la /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs/2.jpg 2>/dev/null || echo MISSING
echo -n "has sale2 shop: "; ls -la /opt/qalaa-runtime/AdminNajaf/App_Data/sales/2/shop.jpg 2>/dev/null || echo MISSING

# Verify via local API auth bypass? Use gateway internal - just file exists + file size
python3 - <<'PY'
import os
root='/opt/qalaa-runtime/AdminNajaf/App_Data'
for p in ['sales/customer-docs/2.jpg','sales/customer-docs/3.jpg','sales/customer-docs/4.jpg','sales/customer-docs/5.jpg','sales/2/shop.jpg','sales/customer-docs/15.jpg','sales/4/shop.jpg']:
  fp=os.path.join(root,p)
  print(p, 'OK' if os.path.isfile(fp) else 'MISS', os.path.getsize(fp) if os.path.isfile(fp) else 0)
# count missing among low ids
miss=0
for i in range(1,50):
  if not os.path.isfile(f'{root}/sales/customer-docs/{i}.jpg'):
    miss+=1
print('missing among 1..49 jpg:', miss)
PY

# quick curl with manager key if present in env files
'''

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        _, stdout, stderr = client.exec_command(remote, timeout=600)
        out = stdout.read().decode("utf-8", errors="replace")
        err = stderr.read().decode("utf-8", errors="replace")
        print(out[-12000:])
        if err.strip():
            print("STDERR:", err[-2000:])
        return stdout.channel.recv_exit_status()
    finally:
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
