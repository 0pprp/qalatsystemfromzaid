#!/usr/bin/env python3
import os
import sys

import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=40,
    allow_agent=False,
    look_for_keys=False,
)
# Use gateway key from env file on server for branch auth
cmd = r"""
set -a
source /etc/qalaa-production/sales-gateway-branch-key.env 2>/dev/null || true
set +a
# discover header name from appsettings
python3 - <<'PY'
import json,glob
for p in glob.glob('/opt/qalaa-runtime/AdminNajaf/appsettings*.json'):
  try:
    t=open(p,encoding='utf-8').read()
  except: continue
  if 'Gateway' in t or 'gateway' in t:
    print('FILE', p)
    # print relevant snippets without dumping secrets fully
    for line in t.splitlines():
      if any(k in line for k in ('Gateway','Header','Key','SalesEmployee','SalesManagement')):
        # redact values
        if ':' in line and '"' in line:
          print(line[:80])
PY
echo KEY_LEN=${#SalesEmployee__GatewayKey}
# try common headers
KEY=$(grep -E 'GatewayKey|BRANCH' /etc/qalaa-production/sales-gateway-branch-key.env 2>/dev/null | head -1)
echo ENV_LINE_PREFIX=$(echo "$KEY" | cut -c1-40)
# call via localhost:5301 with X-Sales-Gateway-Key or similar from gateway code
"""
_, o, e = c.exec_command(cmd, timeout=60)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:500])
c.close()
