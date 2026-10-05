#!/usr/bin/env python3
import os
import subprocess
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
cmd = r"""
python3 <<'PY'
from pathlib import Path
import subprocess
p=Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll')
data=p.read_bytes()
for s in ['PeriodAggregate','EmptyAggregate','EmployeeTrendAsync','DailyListScores','PeriodIgnored']:
    print(s, 'utf8', s.encode() in data, 'utf16', s.encode('utf-16-le') in data)
print('size', p.stat().st_size)
print(subprocess.getoutput('systemctl show qalaa-company-sharenewnajaf -p WorkingDirectory -p ExecStart --no-pager'))
print(subprocess.getoutput('find /opt/qalaa-runtime /opt/qalaa-demo -name BE_Company.dll 2>/dev/null'))
PY
"""
_, o, e = c.exec_command(cmd, timeout=60)
print(o.read().decode("utf-8", "replace"))
err = e.read().decode("utf-8", "replace")
if err.strip():
    print("ERR", err[:800])
c.close()
