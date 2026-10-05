#!/usr/bin/env python3
"""Inspect + reset Kufa new-customer payment, WrongSale, and first-collection eval."""
from __future__ import annotations

import os
import re
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

def grab_cs(cs, key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

def sqlcmd_bin():
    return '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run_sql(server, db, user, pwd, sql):
    p = subprocess.run(
        [sqlcmd_bin(), '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    out = (p.stdout or '') + (('\n' + p.stderr) if p.stderr else '')
    return out.strip()

najaf = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
ncs = najaf['ConnectionStrings']['DataBaseConnection']
ns, ndb, nu, np = grab_cs(ncs,'Data Source') or grab_cs(ncs,'Server'), grab_cs(ncs,'Initial Catalog') or grab_cs(ncs,'Database'), grab_cs(ncs,'User ID') or grab_cs(ncs,'User Id'), grab_cs(ncs,'Password')
print('NAJAF_DB', ndb)

# Find gateway cfg
gw = None
for cand in [
    '/opt/qalaa-runtime/SalesGateway/appsettings.json',
    '/opt/qalaa-runtime/BE_SalesEmployee/appsettings.json',
    '/opt/qalaa-demo/sales-gateway/appsettings.json',
]:
    if os.path.isfile(cand):
        gw = cand
        break
print('GW_CFG', gw)
gcs = ''
if gw:
    gcfg = json.loads(Path(gw).read_text(encoding='utf-8'))
    gcs = (gcfg.get('ConnectionStrings') or {}).get('DataBaseConnection') or (gcfg.get('ConnectionStrings') or {}).get('DefaultConnection') or ''
    # sometimes nested
    if not gcs:
        for v in (gcfg.get('ConnectionStrings') or {}).values():
            if isinstance(v, str) and 'Initial Catalog' in v or (isinstance(v, str) and 'Database=' in v):
                gcs = v
                break
gs = gdb = gu = gp = ''
if gcs:
    gs, gdb, gu, gp = grab_cs(gcs,'Data Source') or grab_cs(gcs,'Server'), grab_cs(gcs,'Initial Catalog') or grab_cs(gcs,'Database'), grab_cs(gcs,'User ID') or grab_cs(gcs,'User Id'), grab_cs(gcs,'Password')
    print('GW_DB', gdb)

print('=== DFCE recent delegate 7 ===')
print(run_sql(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT Id, CustomerId, DelegateId, ListId, Rating,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Note,N''),50) AS Note,
  LEFT(ISNULL(DelegateName,N''),30) AS DelegateName
FROM dbo.DelegateFirstCollectionEvaluations
WHERE DelegateId=7 OR CreatedAtUtc >= DATEADD(day,-2,SYSUTCDATETIME())
ORDER BY CreatedAtUtc DESC;
"""))

print('=== Customers cols sample ===')
print(run_sql(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT TOP 30 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME=N'Customers' ORDER BY ORDINAL_POSITION;
"""))

print('=== Payments cols ===')
print(run_sql(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT TOP 40 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME=N'CustomersPayments' ORDER BY ORDINAL_POSITION;
"""))

print('=== PaymentsRequest cols ===')
print(run_sql(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT TOP 40 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME=N'CustomersPaymentsRequest' ORDER BY ORDINAL_POSITION;
"""))

# Prefer customer 53388 from prior ops; also list any DFCE for delegate 7
print('=== Target customers from DFCE (delegate 7 last 3d) ===')
print(run_sql(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT e.CustomerId, c.CustomerName, e.Rating,
  CONVERT(varchar(19), e.CreatedAtUtc, 120) AS EvalAt
FROM dbo.DelegateFirstCollectionEvaluations e
LEFT JOIN dbo.Customers c ON c.CustomerID = e.CustomerId
WHERE e.DelegateId = 7 AND e.CreatedAtUtc >= DATEADD(day,-3,SYSUTCDATETIME())
ORDER BY e.CreatedAtUtc DESC;
"""))

cust_ids = [53388]
# also pull from DFCE dynamically
raw = run_sql(ns, ndb, nu, np, """
SET NOCOUNT ON;
SELECT CustomerId FROM dbo.DelegateFirstCollectionEvaluations
WHERE DelegateId=7 AND CreatedAtUtc >= DATEADD(day,-3,SYSUTCDATETIME());
""")
for line in raw.splitlines():
    line=line.strip()
    if line.isdigit():
        cid=int(line)
        if cid not in cust_ids:
            cust_ids.append(cid)
print('CUST_IDS', cust_ids)

for cid in cust_ids:
    print(f'=== CUSTOMER {cid} ===')
    print(run_sql(ns, ndb, nu, np, f"""
SET NOCOUNT ON;
SELECT TOP 1 CustomerID, CustomerName, DelegateID,
  CONVERT(varchar(19), LastPaymentDate, 120) AS LastPaymentDate,
  ISNULL(CountReceipt,0) AS CountReceipt
FROM dbo.Customers WHERE CustomerID={cid};
"""))
    print('--- payments ---')
    print(run_sql(ns, ndb, nu, np, f"""
SET NOCOUNT ON;
SELECT TOP 20 *
FROM dbo.CustomersPayments WHERE CustomerID={cid}
ORDER BY 1 DESC;
"""))
    print('--- payment requests ---')
    print(run_sql(ns, ndb, nu, np, f"""
SET NOCOUNT ON;
SELECT TOP 20 *
FROM dbo.CustomersPaymentsRequest WHERE CustomerID={cid}
ORDER BY 1 DESC;
"""))

if gdb:
    print('=== GW WrongSale recent / 53388 ===')
    print(run_sql(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT TOP 20 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME=N'SalesExceptionRequests' ORDER BY ORDINAL_POSITION;
"""))
    print(run_sql(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT TOP 30 RequestId, CustomerId, CustomerName, CityValue, Status,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  LEFT(ISNULL(Notes,N''),80) AS Notes
FROM dbo.SalesExceptionRequests
WHERE CustomerId IN (53388,53385)
   OR Notes LIKE N'%مبيعات خاطئة%'
   OR Notes LIKE N'%تقييم المتجر%'
ORDER BY CreatedAtUtc DESC;
"""))

if not APPLY:
    print('DRY_RUN — pass --apply to mutate')
else:
    print('=== APPLY RESET ===')
    for cid in cust_ids:
        print(run_sql(ns, ndb, nu, np, f"""
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.CustomersPaymentsRequest WHERE CustomerID={cid};
DELETE FROM dbo.CustomersPayments WHERE CustomerID={cid};
DELETE FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId={cid};
-- restore new-sale awaiting first collection: no receipt / clear last payment if only from this test
UPDATE dbo.Customers
SET LastPaymentDate = NULL
WHERE CustomerID={cid};
COMMIT;
SELECT 'RESET_OK' AS Msg, {cid} AS CustomerId;
"""))
    if gdb:
        print(run_sql(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM dbo.SalesExceptionRequests
WHERE CustomerId IN (53388,53385)
   OR (Notes LIKE N'%مبيعات خاطئة%' AND CreatedAtUtc >= DATEADD(day,-2,SYSUTCDATETIME()) AND CityValue IN (N'DatabaseCompanyNajaf', N'النجف', N'الكوفة'));
SELECT @@ROWCOUNT AS DeletedWrongSale;
COMMIT;
"""))
    print('=== VERIFY ===')
    for cid in cust_ids:
        print(run_sql(ns, ndb, nu, np, f"""
SET NOCOUNT ON;
SELECT 'DFCE' AS T, COUNT(*) AS C FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId={cid}
UNION ALL SELECT 'PAY', COUNT(*) FROM dbo.CustomersPayments WHERE CustomerID={cid}
UNION ALL SELECT 'PAYREQ', COUNT(*) FROM dbo.CustomersPaymentsRequest WHERE CustomerID={cid};
SELECT CustomerID, CustomerName, CONVERT(varchar(19), LastPaymentDate, 120) AS LastPaymentDate
FROM dbo.Customers WHERE CustomerID={cid};
"""))
    if gdb:
        print(run_sql(gs, gdb, gu, gp, """
SET NOCOUNT ON;
SELECT COUNT(*) AS RemainingWS
FROM dbo.SalesExceptionRequests
WHERE CustomerId IN (53388,53385)
   OR (Notes LIKE N'%مبيعات خاطئة%' AND CreatedAtUtc >= DATEADD(day,-2,SYSUTCDATETIME()));
"""))
print('OK')
'''.replace('__APPLY__', 'True' if APPLY else 'False')

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        path = '/tmp/reset_kufa_new_customer.py'
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
        Path(r'C:\GitHub\Gemini\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_reset.txt').parent.mkdir(parents=True, exist_ok=True)
        Path(r'C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_reset.txt').write_text(out, encoding='utf-8')
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == '__main__':
    raise SystemExit(main())
