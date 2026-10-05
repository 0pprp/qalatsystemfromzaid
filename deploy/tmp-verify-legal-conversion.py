#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=60,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
set -e
WD=$(systemctl show qalaa-company-sharenewnajaf -p WorkingDirectory --value)
echo WD=$WD
# Prefer Listening URI from ss / netstat via MainPID
PID=$(systemctl show qalaa-company-sharenewnajaf -p MainPID --value)
echo PID=$PID
# Extract urls from process cmdline
tr '\0' ' ' < /proc/$PID/cmdline; echo
# Probe common AdminNajaf ports from appsettings if any
APP=$WD/appsettings.json
python3 - <<'PY'
import json,re,urllib.request,ssl
from pathlib import Path
import subprocess,os
wd=os.environ.get('WD') or subprocess.getoutput("systemctl show qalaa-company-sharenewnajaf -p WorkingDirectory --value").strip()
print('wd',wd)
# find port from nginx or previous knowledge - try 5xxx range via ss
out=subprocess.getoutput("ss -lntp | grep -E 'qalaa|BE_Company|dotnet' | head -40")
print(out[:2000])
ports=sorted(set(int(x) for x in re.findall(r':(\d{4,5})\b', out)))
print('ports', ports[:30])
for p in ports[:25]:
    try:
        req=urllib.request.Request(f'http://127.0.0.1:{p}/api/CustomerDecisions/LegalConversionRequests')
        with urllib.request.urlopen(req, timeout=3) as r:
            print('HIT', p, r.status)
    except Exception as e:
        code=getattr(getattr(e,'code',None), '__int__', lambda: None)()
        if hasattr(e,'code'):
            print('HIT', p, e.code)
        else:
            pass
print('DONE_PROBE')
PY
"""
_, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace"))
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[:800])
c.close()
