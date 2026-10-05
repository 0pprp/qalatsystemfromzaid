#!/usr/bin/env python3
import os, sys, json
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
set +e
# find gateway
find /opt/qalaa-runtime /opt/qalaa-demo -name 'BE_SalesEmployee.dll' 2>/dev/null | head -10
find /opt -name 'appsettings.json' 2>/dev/null | xargs grep -l InternalApiKey 2>/dev/null | head -10

KEY=$(python3 - <<'PY'
import json,glob
for p in glob.glob('/opt/**/appsettings.json', recursive=True):
  try:
    d=json.load(open(p))
  except Exception:
    continue
  if d.get('InternalApiKey'):
    print(d['InternalApiKey'])
    print('FROM', p, file=__import__('sys').stderr)
    break
PY
)
echo "KEY_LEN=${#KEY}"

# call najaf directly
for path in \
  "sales-manager/customer-documents/2/file" \
  "sales-manager/sales/2/shop-image" \
  "sales-manager/customer-documents/15/file"; do
  code=$(curl -s -o /tmp/img.bin -w '%{http_code} %{size_download} %{content_type}' \
    -H "X-Sales-Gateway-Key: $KEY" \
    -H "X-Sales-Manager-Name-B64: 2Ygg2K/YqQ==" \
    "http://127.0.0.1:5301/api/$path")
  echo "$path -> $code magic=$(xxd -p -l 3 /tmp/img.bin)"
done
'''
_,o,e=c.exec_command(remote, timeout=90)
print(o.read().decode('utf-8', errors='replace'))
print(e.read().decode('utf-8', errors='replace')[-2000:])
c.close()
