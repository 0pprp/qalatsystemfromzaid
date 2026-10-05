#!/usr/bin/env python3
import os, json, paramiko, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

cmds = [
    # What does GetAdmin return for city values?
    "curl -s http://defaultdata.alsaaeidy.com/GetAdmin | head -c 4000",
    # nginx: decode which branch city=1 maps to - check gateway logs around profile
    "journalctl -u qalaa-sales-gateway --since '02:08:40' --until '02:09:00' --no-pager 2>/dev/null | grep -iE 'customer-documents|shop-image|profile|53327|sharenew' | head -60",
    # Check SQL for docs 2-5 and sale 2 on Najaf
    r"""python3 - <<'PY'
import json,glob
# try find connection string
import re
p='/opt/qalaa-runtime/AdminNajaf/appsettings.json'
print(open(p).read()[:500])
PY""",
]
for cmd in cmds:
    print("====", cmd[:100])
    _,o,e=c.exec_command(cmd, timeout=60)
    out=o.read().decode("utf-8", errors="replace")
    print(out[-5000:])
c.close()
