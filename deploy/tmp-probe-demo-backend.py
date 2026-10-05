#!/usr/bin/env python3
import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=30,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
echo WD=$(systemctl show qalaa-demo-backend -p WorkingDirectory --value)
ls -la /opt/qalaa-demo/backend/BE_Company.dll 2>&1 | head -2
python3 - <<'PY'
import glob, os
for d in ['/opt/qalaa-demo/backend'] + sorted(glob.glob('/opt/qalaa-runtime/Admin*')):
    cfg=os.path.join(d,'appsettings.json')
    if not os.path.isfile(cfg): continue
    try: t=open(cfg,encoding='utf-8',errors='replace').read()
    except Exception: continue
    if 'DatabaseCompanyNajaf_DEMO' in t or 'Najaf_DEMO' in t:
        print('HIT', d)
PY
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=60)
print(stdout.read().decode("utf-8", "replace"))
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("ERR", err[:400])
c.close()
