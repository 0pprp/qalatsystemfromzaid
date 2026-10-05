#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
set +e
echo "=== Najaf after partial restore ==="
ls -la /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs/2.jpg /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs/5.jpg /opt/qalaa-runtime/AdminNajaf/App_Data/sales/2/shop.jpg 2>&1
echo -n "customer-docs count: "; ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs | wc -l

# Continue restore for remaining admins more carefully
for dest in /opt/qalaa-runtime/Admin*; do
  [ -d "$dest/App_Data" ] || continue
  base=$(basename "$dest")
  case "$base" in *backup*|*old*|*.bak*) continue;; esac
  mkdir -p "$dest/App_Data/sales/customer-docs"
  before=$(ls "$dest/App_Data/sales/customer-docs" 2>/dev/null | wc -l)

  # migration backup
  mig="/opt/qalaa-migration/backups/20260912_190813/$base/App_Data/sales"
  if [ -d "$mig" ]; then
    rsync -a --ignore-existing "$mig"/ "$dest/App_Data/sales/" 2>/dev/null
  fi

  # runtime bak folders
  for bak in /opt/qalaa-runtime/${base}.bak-*/App_Data/sales; do
    [ -d "$bak" ] || continue
    rsync -a --ignore-existing "$bak"/ "$dest/App_Data/sales/" 2>/dev/null
  done

  # qalaa-backups copies that contain App_Data
  for bak in /opt/qalaa-backups/${base}-pre-return-* /opt/qalaa-backups/${base}-pre-bulk-*; do
    [ -d "$bak/App_Data/sales" ] || continue
    rsync -a --ignore-existing "$bak/App_Data/sales"/ "$dest/App_Data/sales/" 2>/dev/null
  done

  after=$(ls "$dest/App_Data/sales/customer-docs" 2>/dev/null | wc -l)
  echo "$base docs $before -> $after"
done

echo "=== verify Najaf files again ==="
python3 - <<'PY'
import os
root='/opt/qalaa-runtime/AdminNajaf/App_Data'
for p in ['sales/customer-docs/2.jpg','sales/customer-docs/3.jpg','sales/customer-docs/4.jpg','sales/customer-docs/5.jpg','sales/customer-docs/15.jpg','sales/2/shop.jpg','sales/4/shop.jpg']:
  fp=os.path.join(root,p)
  print(p, 'OK' if os.path.isfile(fp) else 'MISS', os.path.getsize(fp) if os.path.isfile(fp) else 0)
miss=sum(1 for i in range(1,50) if not os.path.isfile(f'{root}/sales/customer-docs/{i}.jpg'))
print('missing 1..49:', miss, 'total docs', len(os.listdir(f'{root}/sales/customer-docs')))
PY

# Hit API through localhost with internal manager key from appsettings if any
python3 - <<'PY'
import json,urllib.request
# read gateway and try unauthenticated - expect 401; instead use file size as proof
print('file proof done')
PY
'''
_,o,e=c.exec_command(remote, timeout=600)
print(o.read().decode('utf-8', errors='replace')[-10000:])
err=e.read().decode('utf-8', errors='replace')
if err.strip():
    print('STDERR', err[-1500:])
print('exit', o.channel.recv_exit_status())
c.close()
