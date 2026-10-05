#!/usr/bin/env python3
"""Check why كرار 53385 may/may not appear in pending sales; scan demo DB too."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, os, re, subprocess
from pathlib import Path

def parse_cs(cs: str):
    def grab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id') or grab('UID'), grab('Password') or grab('Pwd')

sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(server, db, user, pwd, sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    print(p.stdout or '')
    if p.stderr: print(p.stderr)
    return p.returncode

# --- Najaf live ---
cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('LIVE', db)

print('=== customer 53385 detail ===')
run(server, db, user, pwd, """
SET NOCOUNT ON;
SELECT c.CustomerID, c.DelegateID, c.AmountRemaining, c.AmountTotalSales,
  CONVERT(VARCHAR(80), d.DelegateName) AS ListName
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE c.CustomerID = 53385;

SELECT TOP 5 CustomerSaleID, DateCreate, AmountTotalSales, AmountDaySales
FROM dbo.CustomersSales WHERE CustomerID = 53385 ORDER BY DateCreate DESC;

SELECT TOP 5 SaleId, Status, PostingStatus, PostedAtUtc
FROM dbo.SalesDrafts WHERE CustomerId = 53385 ORDER BY SaleId DESC;

-- pending-sales style eligibility (simplified)
SELECT COUNT(*) AS AuthRows FROM dbo.FollowerSaleAuthentications WHERE CustomerId = 53385;
""")

print('=== WrongSaleReports / SalesRequests ===')
run(server, db, user, pwd, """
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.WrongSaleReports',N'U') IS NOT NULL
  SELECT TOP 20 Id, CustomerId, CONVERT(CONVERT(VARCHAR(120), CustomerName), '') AS NameA, Rating, Status, CreatedAtUtc
  FROM dbo.WrongSaleReports ORDER BY CreatedAtUtc DESC;
ELSE PRINT 'no WrongSaleReports';

IF COL_LENGTH(N'dbo.SalesRequests', N'IsWrongSale') IS NOT NULL
  SELECT TOP 10 Id, ExistingCustomerId, IsWrongSale, ShopRating, CreatedAtUtc,
    CONVERT(VARCHAR(80), FullName) AS NameA
  FROM dbo.SalesRequests WHERE IsWrongSale = 1 OR ShopRating <= 6
  ORDER BY CreatedAtUtc DESC;
""")

# --- demo follower backend ---
demo = Path('/opt/qalaa-demo/follower-backend/appsettings.json')
if demo.is_file():
    dcfg = json.loads(demo.read_text(encoding='utf-8'))
    ds, ddb, du, dp = parse_cs(dcfg['ConnectionStrings']['DataBaseConnection'])
    print('DEMO', ddb)
    run(ds, ddb, du, dp, """
SET NOCOUNT ON;
SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerSaleAuthentications',N'U') IS NULL THEN 0 ELSE 1 END AS HasTable;
IF OBJECT_ID(N'dbo.FollowerSaleAuthentications',N'U') IS NOT NULL
BEGIN
  SELECT COUNT(*) AS AuthCount FROM dbo.FollowerSaleAuthentications;
  SELECT TOP 30 Id, CustomerId, ListId, Rating, FollowerUserId,
    CONVERT(VARCHAR(80), FollowerName) AS FollowerA,
    CONVERT(VARCHAR(120), (SELECT TOP 1 CustomerName FROM dbo.Customers c WHERE c.CustomerID = a.CustomerId)) AS CustA,
    AuthenticatedAtUtc
  FROM dbo.FollowerSaleAuthentications a
  ORDER BY AuthenticatedAtUtc DESC;
END
""")
else:
    print('NO_DEMO_CFG')

# unicode hex of name for 53385
print('=== name hex 53385 ===')
run(server, db, user, pwd, """
SELECT CustomerID,
  CONVERT(VARCHAR(MAX), CAST(CustomerName AS VARBINARY(200)), 1) AS NameHex
FROM dbo.Customers WHERE CustomerID = 53385;
""")
print('DONE')
'''


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required"); return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    _, stdout, stderr = c.exec_command(f"python3 - <<'PY'\n{REMOTE}\nPY")
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    code = stdout.channel.recv_exit_status()
    path = Path(__file__).resolve().parent.parent / "_ops_tmp_krar_pending_out.txt"
    path.write_text(out + ("\n" + err if err.strip() else ""), encoding="utf-8")
    for line in out.splitlines():
        print(line.encode("ascii", errors="replace").decode("ascii"))
    print("FULL_LOG", path)
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
