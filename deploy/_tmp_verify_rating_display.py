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
_, o, e = c.exec_command(
    r"""
ls -lt /opt/qalaa-runtime/AdminFrontend/assets/sales-manager-employees*.js | head -5
python3 - <<'PY'
from pathlib import Path
assets=Path('/opt/qalaa-runtime/AdminFrontend/assets')
files=sorted(assets.glob('sales-manager-employees*.js'), key=lambda p: p.stat().st_mtime, reverse=True)
for p in files[:3]:
    t=p.read_text(encoding='utf-8', errors='replace')
    has_count = ' · ' in t or '${score}' in t or 'ratedCustomerCount' in t and 'score} ·' in t
    # look for pattern that appends customer count
    bad = '`${score} · ${n}`' in t or "score + ' · '" in t or '· ${n}' in t
    print(p.name, 'mtime', int(p.stat().st_mtime), 'bad_concat', bad, 'toFixed', 'toFixed(2)' in t)
idx=Path('/opt/qalaa-runtime/AdminFrontend/index.html').read_text(encoding='utf-8', errors='replace')
print('index refs employees', 'sales-manager-employees' in idx)
PY
""",
    timeout=60,
)
print(o.read().decode("utf-8", "replace"))
print(e.read().decode("utf-8", "replace")[:300])
c.close()
