#!/usr/bin/env python3
"""Reset Kufa test: delete latest payment + DFCE + WrongSale; keep older payment if any."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
APPLY = "--apply" in sys.argv


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = r'''
import json, os, re, subprocess
from pathlib import Path

APPLY = __APPLY__

def grab(cs, key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

def sqlbin():
    return '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(server, db, user, pwd, sql):
    p = subprocess.run(
        [sqlbin(), '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    return ((p.stdout or '') + (('\n'+p.stderr) if p.stderr else '')).strip()

najaf = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
ncs = najaf['ConnectionStrings']['DataBaseConnection']
ns, ndb, nu, np = grab(ncs,'Data Source') or grab(ncs,'Server'), grab(ncs,'Initial Catalog') or grab(ncs,'Database'), grab(ncs,'User ID') or grab(ncs,'User Id'), grab(ncs,'Password')

# locate gateway / exception DB
gw_paths = []
for root, dirs, files in os.walk('/opt/qalaa-runtime'):
    if 'appsettings.json' in files and ('Sales' in root or 'Gateway' in root or 'BE_Sales' in root):
        gw_paths.append(os.path.join(root, 'appsettings.json'))
for root, dirs, files in os.walk('/etc/qalaa-production'):
    pass
print('GW_CANDIDATES', gw_paths[:20])

# also check env files for connection
env_hits = subprocess.getoutput("grep -Ril 'SalesException\\|ConnectionStrings' /etc/qalaa-production 2>/dev/null | head -20")
print('ENV_HITS', env_hits)

# systemd unit Environment for sales gateway
print('UNIT', subprocess.getoutput('systemctl cat qalaa-sales-gateway 2>/dev/null | head -80'))

gs=gdb=gu=gp=''
for pth in gw_paths:
    try:
        cfg=json.loads(Path(pth).read_text(encoding='utf-8'))
    except Exception:
        continue
    cs=(cfg.get('ConnectionStrings') or {})
    for k,v in cs.items():
        if not isinstance(v, str):
            continue
        # try query SalesExceptionRequests
        s,d,u,pw = grab(v,'Data Source') or grab(v,'Server'), grab(v,'Initial Catalog') or grab(v,'Database'), grab(v,'User ID') or grab(v,'User Id'), grab(v,'Password')
        if not (s and d and u and pw):
            continue
        probe = run(s,d,u,pw, "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.SalesExceptionRequests',N'U') IS NULL THEN N'NO' ELSE N'YES' END;")
        print('PROBE', pth, d, probe.replace('\\n',' ')[:120])
        if 'YES' in probe:
            gs,gdb,gu,gp=s,d,u,pw
            print('GW_DB_OK', d, 'from', pth)
            break
    if gdb:
        break

# Also try common company/gateway DBs from known services
if not gdb:
    for cand in [
        '/opt/qalaa-runtime/CompanyNajaf/appsettings.json',
        '/opt/qalaa-runtime/ShortNewNajaf/appsettings.json',
    ]:
        pass

print('=== payments 53388 detail ===')
print(run(ns,ndb,nu,np,"""
SET NOCOUNT ON;
SELECT CustomerPaymentID, CustomerID, DelegateID, BoundNumber,
  CONVERT(varchar(23), PaymentDate, 121) AS PaymentDate,
  CONVERT(varchar(23), CreatedDate, 121) AS CreatedDate,
  ClientPaymentId, AsyncID, AccountZero
FROM dbo.CustomersPayments
WHERE CustomerID=53388
ORDER BY PaymentDate DESC;
"""))

print('=== customer 53388 ===')
print(run(ns,ndb,nu,np,"""
SET NOCOUNT ON;
SELECT CustomerID, CustomerName, DelegateID, CustomerState, IsFakeSale, SaleName
FROM dbo.Customers WHERE CustomerID=53388;
"""))

print('=== sales for 53388 ===')
print(run(ns,ndb,nu,np,"""
SET NOCOUNT ON;
SELECT TOP 5 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME LIKE N'%Sale%' AND COLUMN_NAME LIKE N'%Customer%' ORDER BY TABLE_NAME;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' AND (TABLE_NAME LIKE N'%Sale%Request%' OR TABLE_NAME LIKE N'%CustomersSale%' OR TABLE_NAME=N'Sales') ORDER BY 1;
"""))

if gdb:
    print('=== WrongSale rows ===')
    print(run(gs,gdb,gu,gp,"""
SET NOCOUNT ON;
SELECT TOP 40 RequestId, CustomerId, CustomerName, CityValue, Status, ExceptionType,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Notes,N''),100) AS Notes
FROM dbo.SalesExceptionRequests
WHERE CustomerId=53388
   OR (CreatedAtUtc >= DATEADD(day,-2,SYSUTCDATETIME()) AND (Notes LIKE N'%مبيعات خاطئة%' OR Notes LIKE N'%تقييم المتجر%' OR ExceptionType LIKE N'%Wrong%'))
ORDER BY CreatedAtUtc DESC;
"""))

# Target: delete ONLY the test payment that matches DFCE time / has ClientPaymentId from device
# Keep older payment 419005
PAY_DEL = 419158
print('WILL_DELETE_PAYMENT', PAY_DEL)
print('WILL_DELETE_DFCE CustomerId=53388')
print('WILL_DELETE_WRONG_SALE CustomerId=53388')

if not APPLY:
    print('DRY_RUN')
else:
    print('APPLY')
    print(run(ns,ndb,nu,np,f"""
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.CustomersPaymentsRequest WHERE CustomerID=53388 AND ClientPaymentId=N'17fb84bf-b0b1-4b44-91e9-baba5ee4ff5b';
DELETE FROM dbo.CustomersPayments WHERE CustomerPaymentID={PAY_DEL};
DELETE FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
COMMIT;
SELECT 'NAJAF_DONE' AS Msg;
SELECT COUNT(*) AS PayLeft FROM dbo.CustomersPayments WHERE CustomerID=53388;
SELECT COUNT(*) AS DfceLeft FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
SELECT COUNT(*) AS PayReqLeft FROM dbo.CustomersPaymentsRequest WHERE CustomerID=53388;
"""))
    if gdb:
        # discover PK/id column
        cols = run(gs,gdb,gu,gp,"""
SET NOCOUNT ON;
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'SalesExceptionRequests';
""")
        print('WS_COLS', cols)
        print(run(gs,gdb,gu,gp,"""
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.SalesExceptionRequests WHERE CustomerId=53388;
SELECT @@ROWCOUNT AS DeletedWS;
COMMIT;
SELECT COUNT(*) AS RemainingWS FROM dbo.SalesExceptionRequests WHERE CustomerId=53388;
"""))
print('OK')
'''.replace('__APPLY__', 'True' if APPLY else 'False')

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        path = '/tmp/reset_kufa_apply.py'
        sftp = c.open_sftp()
        with sftp.file(path, 'w') as f:
            f.write(remote)
        sftp.close()
        _, o, e = c.exec_command(f'python3 {path}', timeout=180)
        out = o.read().decode('utf-8', 'replace')
        err = e.read().decode('utf-8', 'replace')
        print(out)
        if err.strip():
            print('STDERR', err[:2500], file=sys.stderr)
        Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_reset2.txt').write_text(out, encoding='utf-8')
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == '__main__':
    raise SystemExit(main())
