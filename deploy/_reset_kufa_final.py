#!/usr/bin/env python3
"""Apply Kufa reset: payment 419158 + DFCE 53388 + WrongSale for 53388."""
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
    return ((p.stdout or '') + (('\n' + p.stderr) if p.stderr else '')).strip()

# Najaf company DB (delegate)
najaf = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
ncs = najaf['ConnectionStrings']['DataBaseConnection']
ns = grab(ncs,'Data Source') or grab(ncs,'Server')
ndb = grab(ncs,'Initial Catalog') or grab(ncs,'Database')
nu = grab(ncs,'User ID') or grab(ncs,'User Id')
np = grab(ncs,'Password')

# Gateway exception DB from running process env
pid = subprocess.getoutput('systemctl show -p MainPID --value qalaa-sales-gateway').strip()
gcs = ''
for b in Path(f'/proc/{pid}/environ').read_bytes().split(b'\0'):
    s = b.decode('utf-8','replace')
    if s.startswith('ConnectionStrings__SalesGateway='):
        gcs = s.split('=',1)[1]
        break
gs = grab(gcs,'Data Source') or grab(gcs,'Server') or 'localhost'
gdb = grab(gcs,'Initial Catalog') or grab(gcs,'Database')
gu = grab(gcs,'User ID') or grab(gcs,'User Id')
gp = grab(gcs,'Password')
print('NAJAF', ndb, 'GW', gdb, 'user', gu)

print('=== WS schema ===')
print(run(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME=N'SalesExceptionRequests' ORDER BY ORDINAL_POSITION;
"""))

print('=== WS for 53388 / recent WrongSale ===')
print(run(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT TOP 30 Id, CustomerId, CustomerName, CityValue, Status, Kind,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Reason,N''),100) AS Reason
FROM dbo.SalesExceptionRequests
WHERE CustomerId=53388 OR CustomerId=53385
ORDER BY CreatedAtUtc DESC;
"""))

# try alternate column names if Kind/Reason fail
print('=== WS select * top ===')
print(run(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT TOP 5 * FROM dbo.SalesExceptionRequests WHERE CustomerId=53388 ORDER BY 1 DESC;
SELECT TOP 10 * FROM dbo.SalesExceptionRequests ORDER BY 1 DESC;
"""))

print('=== before payments ===')
print(run(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT CustomerPaymentID, BoundNumber, CONVERT(varchar(19), PaymentDate, 120) AS PaymentDate, ClientPaymentId
FROM dbo.CustomersPayments WHERE CustomerID=53388 ORDER BY PaymentDate DESC;
SELECT Id, Rating, CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc
FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
"""))

if not APPLY:
    print('DRY_RUN only')
else:
    print('APPLYING')
    print(run(ns, ndb, nu, np, """
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.CustomersPaymentsRequest
WHERE CustomerID=53388 AND ClientPaymentId=N'17fb84bf-b0b1-4b44-91e9-baba5ee4ff5b';
DELETE FROM dbo.CustomersPayments WHERE CustomerPaymentID=419158;
DELETE FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
COMMIT;
SELECT 'NAJAF_OK' AS Msg;
SELECT CustomerPaymentID, BoundNumber, CONVERT(varchar(19), PaymentDate, 120) AS PaymentDate
FROM dbo.CustomersPayments WHERE CustomerID=53388 ORDER BY PaymentDate DESC;
SELECT COUNT(*) AS DfceLeft FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
"""))
    print(run(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.SalesExceptionRequests WHERE CustomerId=53388;
SELECT @@ROWCOUNT AS DeletedWS;
COMMIT;
SELECT COUNT(*) AS RemainingWS FROM dbo.SalesExceptionRequests WHERE CustomerId=53388;
"""))
print('DONE')
'''.replace('__APPLY__', 'True' if APPLY else 'False')

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        path = '/tmp/reset_kufa_final.py'
        sftp = c.open_sftp()
        with sftp.file(path, 'w') as f:
            f.write(remote)
        sftp.close()
        _, o, e = c.exec_command(f'python3 {path}', timeout=180)
        out = o.read().decode('utf-8', 'replace')
        err = e.read().decode('utf-8', 'replace')
        print(out)
        if err.strip():
            print('STDERR', err[:2000], file=sys.stderr)
        Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_reset3.txt').write_text(out, encoding='utf-8')
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == '__main__':
    raise SystemExit(main())
