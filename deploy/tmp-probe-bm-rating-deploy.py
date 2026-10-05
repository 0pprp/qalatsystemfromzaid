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
set +e
echo ACTIVE=$(systemctl is-active qalaa-demo-backend)
journalctl -u qalaa-demo-backend -n 50 --no-pager
echo '---HTTP---'
curl -s -o /tmp/root.html -w 'root=%{http_code}\n' http://127.0.0.1:8080/
curl -s -o /tmp/login.json -w 'login=%{http_code}\n' -H 'Content-Type: application/json' -d '{"UserName":"x","Password":"y"}' http://127.0.0.1:8080/api/Users/Users_LoginAdmin
head -c 500 /tmp/login.json; echo
python3 - <<'PY'
from pathlib import Path
p = Path('/opt/qalaa-demo/backend/BE_Company.dll')
print('dll_size', p.stat().st_size)
data = p.read_bytes()
for s in [b'ListRatingTrendCalculator', 'متوسط تقييم القائمة'.encode('utf-8'), b'BranchManagerStatsService']:
    print(s.decode('utf-8', 'replace')[:40], 'FOUND' if s in data else 'MISSING')
PY
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace")[-8000:])
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("ERR", err[-1500:])
c.close()
