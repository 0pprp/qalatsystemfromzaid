#!/usr/bin/env python3
import json, re, os
from pathlib import Path
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

remote = r'''
python3 <<'PY'
import json, re
from pathlib import Path
try:
    import pymssql
except Exception as e:
    print('NO_PYMSSQL', e)
    raise SystemExit(0)

d = json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
cs = d['ConnectionStrings']['DataBaseConnection']
parts = {p.split('=',1)[0].strip().lower(): p.split('=',1)[1] for p in cs.split(';') if '=' in p}
server = parts.get('data source') or parts.get('server')
db = parts.get('initial catalog') or parts.get('database')
user = parts.get('user id') or parts.get('user')
pwd = parts.get('password') or parts.get('pwd')
conn = pymssql.connect(server=server, user=user, password=pwd, database=db)
cur = conn.cursor()
cur.execute("""
SELECT UserType, COUNT(*)
FROM Users
WHERE UserType IN (N'موظف مبيعات', N'محاسب فرعي', N'مدير فرع', N'مدير مبيعات', N'موظف فلترة المبيعات')
   OR UserType LIKE N'%مبيعات%'
GROUP BY UserType
ORDER BY COUNT(*) DESC
""")
print('USER_TYPES')
for r in cur.fetchall():
    print(repr(r[0]), r[1])
cur.execute("""
IF OBJECT_ID(N'dbo.SalesWorkShifts', N'U') IS NULL
  SELECT 'NO_TABLE' AS x
ELSE
  SELECT TOP 8 Id, EmployeeId, Status, StartedAtUtc, CutoffAtUtc
  FROM dbo.SalesWorkShifts ORDER BY Id DESC
""")
print('SHIFTS')
for r in cur.fetchall():
    print(r)
conn.close()
PY
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
_, o, e = c.exec_command(remote, timeout=60)
print(o.read().decode("utf-8", "replace"))
err = e.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[-1500:])
c.close()
