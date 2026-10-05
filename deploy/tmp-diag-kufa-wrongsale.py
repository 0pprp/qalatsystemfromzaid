#!/usr/bin/env python3
"""Diagnose + backfill missing WrongSale from FirstCollectionEvaluations (Najaf/Kufa)."""
from __future__ import annotations
import json, os, re, subprocess, sys
from pathlib import Path
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
OUT = Path(__file__).resolve().parent.parent / "_ops_tmp_kufa_ws_diag.txt"

REMOTE = r'''
import json, os, re, subprocess
from pathlib import Path

def parse_cs(cs):
    def grab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id') or grab('UID'), grab('Password') or grab('Pwd')

sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run_sql(server, db, user, pwd, sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace')
    return (p.stdout or '') + (('\n'+p.stderr) if p.stderr else '')

# Najaf delegate
cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('DELEGATE_DB', db)
print('SalesGateway', cfg.get('SalesGateway'))
print('Branch', {k: cfg.get('Branch',{}).get(k) if isinstance(cfg.get('Branch'), dict) else cfg.get(k) for k in []})
print('BranchCityValue', cfg.get('Branch', {}).get('CityValue') if isinstance(cfg.get('Branch'), dict) else cfg.get('Branch:CityValue'))
print('InternalApiKeySet', bool(cfg.get('InternalApiKey') or (cfg.get('SalesGateway') or {}).get('ApiKey')))

print('=== FirstCollection evals rating<=6 last 3 days ===')
print(run_sql(server, db, user, pwd, """
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations', N'U') IS NULL
BEGIN SELECT N'NO_TABLE' AS Msg; END
ELSE
BEGIN
SELECT TOP 30 e.Id, e.CustomerId, e.ListId, e.DelegateId, e.Rating, e.EvaluatorName, e.CreatedAtUtc,
  c.CustomerName, d.DelegateName
FROM dbo.DelegateFirstCollectionEvaluations e
LEFT JOIN dbo.Customers c ON c.CustomerID = e.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID = e.DelegateId OR d.DelegateID = e.ListId
WHERE e.Rating <= 6 AND e.CreatedAtUtc >= DATEADD(day, -3, SYSUTCDATETIME())
ORDER BY e.CreatedAtUtc DESC;
END
"""))

print('=== Delegates named كوفة ===')
print(run_sql(server, db, user, pwd, """
SET NOCOUNT ON;
SELECT TOP 20 DelegateID, DelegateName, CityID, UserType
FROM dbo.Delegates
WHERE DelegateName LIKE N'%كوف%' OR DelegateName LIKE N'%الكوفة%';
"""))

# Gateway central exceptions DB
gw = None
for path in ['/opt/qalaa-runtime/SalesGateway/appsettings.json','/opt/qalaa-demo/sales-gateway/appsettings.json']:
    if os.path.isfile(path):
        gw = json.loads(Path(path).read_text(encoding='utf-8')); print('GW_CFG', path); break
if gw:
    cs = gw.get('ConnectionStrings',{}).get('DataBaseConnection') or gw.get('ConnectionStrings',{}).get('DefaultConnection') or ''
    # try several keys
    for k,v in (gw.get('ConnectionStrings') or {}).items():
        print('GW_CS_KEY', k)
    # SalesEmployee often has central DB
print('=== Recent WrongSale exceptions on gateway (if reachable) ===')
# Find SalesEmployee connection
for path in ['/opt/qalaa-runtime/SalesGateway/appsettings.json','/opt/qalaa-runtime/BE_SalesEmployee/appsettings.json']:
    if not os.path.isfile(path):
        continue
    g = json.loads(Path(path).read_text(encoding='utf-8'))
    cdict = g.get('ConnectionStrings') or {}
    for key in ('CentralConnection','SalesEmployee','DataBaseConnection','DefaultConnection','GatewayConnection'):
        if key in cdict:
            s,d,u,pw = parse_cs(cdict[key])
            print('TRY', path, key, d)
            out = run_sql(s,d,u,pw, """
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.SalesExceptionRequests', N'U') IS NULL BEGIN SELECT N'NO_EXC_TABLE' AS Msg; END
ELSE
SELECT TOP 30 Id, CustomerId, CustomerName, CityValue, CityName, Status, ReadAtUtc, RequestingManagerDisplayName, RequestedAtUtc,
  LEFT(Reason, 80) AS ReasonHead
FROM dbo.SalesExceptionRequests
WHERE (Reason LIKE N'%مبيعات خاطئة%' OR Reason LIKE N'%تقييم المتجر%')
  AND RequestedAtUtc >= DATEADD(day, -3, SYSUTCDATETIME())
ORDER BY RequestedAtUtc DESC;
""")
            print(out[:4000])
print('DONE')
'''

def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    _, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{REMOTE}\nPY")
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    code = stdout.channel.recv_exit_status()
    OUT.write_text(out + ("\n"+err if err.strip() else ""), encoding="utf-8")
    print(out.encode("ascii", errors="replace").decode("ascii"))
    c.close()
    return code

if __name__ == "__main__":
    raise SystemExit(main())
