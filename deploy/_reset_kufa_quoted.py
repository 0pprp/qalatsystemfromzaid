#!/usr/bin/env python3
"""Reset Kufa test data with QUOTED_IDENTIFIER ON."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    remote = r'''
import json, os, re, subprocess
from pathlib import Path

def grab(cs, key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

def sqlbin():
    return '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(server, db, user, pwd, sql):
    p = subprocess.run(
        [sqlbin(), '-C', '-f', '65001', '-I', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    return ((p.stdout or '') + (('\n' + p.stderr) if p.stderr else '')).strip()

najaf = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
ncs = najaf['ConnectionStrings']['DataBaseConnection']
ns = grab(ncs,'Data Source') or grab(ncs,'Server')
ndb = grab(ncs,'Initial Catalog') or grab(ncs,'Database')
nu = grab(ncs,'User ID') or grab(ncs,'User Id')
np = grab(ncs,'Password')

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

ws_id = 'AAAE2C37-69AE-4EF6-B2E9-7E0DE516CBE5'

print('=== NAJAF DELETE ===')
print(run(ns, ndb, nu, np, """
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.CustomersPaymentsRequest
WHERE CustomerID=53388 AND ClientPaymentId=N'17fb84bf-b0b1-4b44-91e9-baba5ee4ff5b';
PRINT CONCAT('PayReq deleted=', @@ROWCOUNT);
DELETE FROM dbo.CustomersPayments WHERE CustomerPaymentID=419158;
PRINT CONCAT('Pay deleted=', @@ROWCOUNT);
DELETE FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
PRINT CONCAT('DFCE deleted=', @@ROWCOUNT);
COMMIT;
"""))

print('=== GW DELETE WrongSale + audit ===')
print(run(gs, gdb, gu, gp, f"""
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID(N'dbo.SalesExceptionAudit', N'U') IS NOT NULL
BEGIN
  DELETE FROM dbo.SalesExceptionAudit WHERE ExceptionRequestId = '{ws_id}';
  PRINT CONCAT('Audit deleted=', @@ROWCOUNT);
END
DELETE FROM dbo.SalesExceptionRequests WHERE Id = '{ws_id}' OR CustomerId=53388;
PRINT CONCAT('WS deleted=', @@ROWCOUNT);
COMMIT;
"""))

print('=== VERIFY ===')
print(run(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT CustomerPaymentID, BoundNumber, CONVERT(varchar(19), PaymentDate, 120) AS PaymentDate, ClientPaymentId
FROM dbo.CustomersPayments WHERE CustomerID=53388 ORDER BY PaymentDate DESC;
SELECT COUNT(*) AS DfceLeft FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId=53388;
SELECT COUNT(*) AS PayReqLeft FROM dbo.CustomersPaymentsRequest WHERE CustomerID=53388 AND ClientPaymentId=N'17fb84bf-b0b1-4b44-91e9-baba5ee4ff5b';
"""))
print(run(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT COUNT(*) AS WsLeft FROM dbo.SalesExceptionRequests WHERE CustomerId=53388;
SELECT TOP 3 Id, CustomerId, Status, CONVERT(varchar(19), RequestedAtUtc, 120) AS RequestedAtUtc
FROM dbo.SalesExceptionRequests
WHERE Reason LIKE N'%مبيعات خاطئة%' AND RequestedAtUtc >= DATEADD(day,-2,SYSUTCDATETIME())
ORDER BY RequestedAtUtc DESC;
"""))
print('OK')
'''

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        path = '/tmp/reset_kufa_quoted.py'
        sftp = c.open_sftp()
        with sftp.file(path, 'w') as f:
            f.write(remote)
        sftp.close()
        _, o, e = c.exec_command(f'python3 {path}', timeout=180)
        out = o.read().decode('utf-8', 'replace')
        err = e.read().decode('utf-8', 'replace')
        print(out)
        if err.strip():
            print('STDERR', err[:2000])
        Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_reset4.txt').write_text(out, encoding='utf-8')
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == '__main__':
    raise SystemExit(main())
