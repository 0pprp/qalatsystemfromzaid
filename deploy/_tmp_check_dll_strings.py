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
cmd = r"""
python3 - <<'PY'
from pathlib import Path
p=Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll')
data=p.read_bytes()
needles=[
    b'MetricLabel',
    'متوسط نقاط زبائن الفترة'.encode('utf-8'),
    'متوسط تقييم مبيعات الفترة'.encode('utf-8'),
    b'PeriodAggregate',
    b'EmptyAggregate',
]
for n in needles:
    print(n[:40], 'YES' if n in data else 'NO')
print('mtime', p.stat().st_mtime)
PY
"""
_, o, e = c.exec_command(cmd, timeout=60)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:400])
c.close()
