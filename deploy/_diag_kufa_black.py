#!/usr/bin/env python3
"""Diagnose hung payment + black screen after Kufa eval/pay."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = r'''
import os, json, re, subprocess
from pathlib import Path

cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
cs = cfg['ConnectionStrings']['DataBaseConnection']

def grab(key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

server = grab('Data Source') or grab('Server')
db = grab('Initial Catalog') or grab('Database')
user = grab('User ID') or grab('User Id')
pwd = grab('Password')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    return ((p.stdout or '') + (('\n' + p.stderr) if p.stderr else '')).strip()

print('=== service ===')
print(subprocess.getoutput('systemctl is-active qalaa-delegatehaidernajaflast'))
print(subprocess.getoutput('systemctl is-active qalaa-sales-gateway 2>/dev/null || systemctl is-active qalaa-database-sales-gateway 2>/dev/null || true'))

print('=== journal najaf last 45m errors/pay/eval ===')
print(subprocess.getoutput(
    "journalctl -u qalaa-delegatehaidernajaflast --since '45 minutes ago' --no-pager | "
    "grep -iE 'error|exception|fail|WrongSale|FirstCollection|Payment|timeout|500|400|403|502|NullReference|Object reference' | tail -80 || true"
))

print('=== journal gateway last 45m ===')
print(subprocess.getoutput(
    "journalctl --since '45 minutes ago' --no-pager | "
    "grep -iE 'WrongSale|SalesException|FirstCollection|مبيعات خاطئة' | tail -40 || true"
))

print('=== nginx FirstCollection / Payment recent ===')
print(subprocess.getoutput("grep -rhE 'FirstCollection|CustomersPayments|Payment' /var/log/nginx/*najaf* 2>/dev/null | tail -40 || true"))
print(subprocess.getoutput("grep -rhE 'FirstCollection|/api/.*Payment' /var/log/nginx/access.log /var/log/nginx/access.log.1 2>/dev/null | tail -40 || true"))

print('=== FirstCollection tables ===')
print(run("""
SET NOCOUNT ON;
SELECT 'FirstCollectionEvaluations' AS T, CASE WHEN OBJECT_ID(N'dbo.FirstCollectionEvaluations',N'U') IS NULL THEN N'NO' ELSE N'YES' END AS HasT
UNION ALL
SELECT 'DelegateFirstCollectionEvaluations', CASE WHEN OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations',N'U') IS NULL THEN N'NO' ELSE N'YES' END;
"""))

print('=== recent payments delegate 7 / kufa names ===')
print(run("""
SET NOCOUNT ON;
SELECT TOP 15
  p.CustomerID,
  c.CustomerName,
  p.DelegateID,
  CONVERT(varchar(19), p.PaymentDate, 120) AS PaymentDate,
  p.Amount
FROM dbo.CustomersPayments p
LEFT JOIN dbo.Customers c ON c.CustomerID = p.CustomerID
WHERE p.DelegateID = 7 OR c.City LIKE N'%كوف%' OR c.District LIKE N'%كوف%'
ORDER BY p.PaymentDate DESC;
"""))

print('=== recent requests/evals ===')
print(run("""
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.FirstCollectionEvaluations',N'U') IS NOT NULL
  SELECT TOP 10 * FROM dbo.FirstCollectionEvaluations ORDER BY 1 DESC;
ELSE SELECT N'NO_FCE' AS Msg;
"""))

# try gateway DB for WrongSale
gcfg_path = Path('/opt/qalaa-runtime/DatabaseSalesGateway/appsettings.json')
if not gcfg_path.exists():
    for p in Path('/opt/qalaa-runtime').glob('*Sales*Gateway*/appsettings.json'):
        gcfg_path = p; break
print('gateway cfg', gcfg_path)
if gcfg_path.exists():
    gcfg = json.loads(gcfg_path.read_text(encoding='utf-8'))
    gcs = gcfg.get('ConnectionStrings', {}).get('DataBaseConnection') or gcfg.get('ConnectionStrings', {}).get('DefaultConnection') or ''
    def ggrab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', gcs, re.I)
        return m.group(1) if m else ''
    gs, gdb, gu, gp = ggrab('Data Source') or ggrab('Server'), ggrab('Initial Catalog') or ggrab('Database'), ggrab('User ID') or ggrab('User Id'), ggrab('Password')
    print('=== gateway WrongSale last 2h ===')
    def grun(sql):
        p = subprocess.run(
            [sqlcmd, '-C', '-f', '65001', '-S', gs, '-d', gdb, '-U', gu, '-P', gp, '-W', '-s', '|', '-Q', sql],
            capture_output=True, text=True, encoding='utf-8', errors='replace',
        )
        return ((p.stdout or '') + (('\n' + p.stderr) if p.stderr else '')).strip()
    print(grun("""
SET NOCOUNT ON;
SELECT TOP 15 RequestId, CustomerId, CustomerName, CityValue, CityName, Status,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  RequestedByName, LEFT(ISNULL(Notes,''),80) AS Notes
FROM dbo.SalesExceptionRequests
WHERE Notes LIKE N'%مبيعات خاطئة%' OR Notes LIKE N'%WrongSale%' OR ExceptionType LIKE N'%Wrong%'
ORDER BY CreatedAtUtc DESC;
"""))

print('OK')
'''

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        path = "/tmp/diag_kufa_black.py"
        sftp = c.open_sftp()
        with sftp.file(path, "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 {path}", timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:2000], file=sys.stderr)
        Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_black.txt").write_text(out, encoding="utf-8")
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
