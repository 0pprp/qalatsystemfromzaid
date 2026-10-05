#!/usr/bin/env python3
"""Delete FollowerSaleAuthentications for كرار اركان فليح (CustomerId 53385) on Najaf."""
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

def parse_cs(cs):
    def grab(key):
        m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
        return m.group(1) if m else ''
    return grab('Data Source') or grab('Server'), grab('Initial Catalog') or grab('Database'), grab('User ID') or grab('User Id') or grab('UID'), grab('Password') or grab('Pwd')

sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'
cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('DB', db)

def run(sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    print(p.stdout or '')
    if p.stderr:
        print(p.stderr)
    return p.returncode

print('=== BEFORE ===')
run("""
SET NOCOUNT ON;
SELECT a.Id, a.CustomerId, a.ListId, a.Rating, a.FollowerUserId, a.AuthenticatedAtUtc
FROM dbo.FollowerSaleAuthentications a
WHERE a.CustomerId = 53385;
SELECT COUNT(*) AS AuthCountAll FROM dbo.FollowerSaleAuthentications;
""")

print('=== DELETE ===')
run("""
SET NOCOUNT ON;
DELETE FROM dbo.FollowerSaleAuthentications WHERE CustomerId = 53385;
PRINT CONCAT('DELETED_AUTH=', @@ROWCOUNT);

DELETE a
FROM dbo.FollowerSaleAuthentications a
INNER JOIN dbo.Customers c ON c.CustomerID = a.CustomerId
WHERE c.CustomerName LIKE N'%كرار%' AND c.CustomerName LIKE N'%اركان%';
PRINT CONCAT('DELETED_BY_NAME=', @@ROWCOUNT);
""")

print('=== AFTER ===')
run("""
SET NOCOUNT ON;
SELECT COUNT(*) AS AuthLeft FROM dbo.FollowerSaleAuthentications WHERE CustomerId = 53385;
SELECT c.CustomerID, c.DelegateID,
  CASE WHEN a.Id IS NULL THEN N'NO_AUTH' ELSE N'STILL_AUTH' END AS AuthState,
  CASE WHEN sd.SaleId IS NULL THEN 0 ELSE 1 END AS HasPostedDraft
FROM dbo.Customers c
OUTER APPLY (
  SELECT TOP 1 SaleId FROM dbo.SalesDrafts
  WHERE CustomerId = c.CustomerID AND Status = N'Completed' AND ISNULL(PostingStatus, N'') = N'Posted'
) sd
LEFT JOIN dbo.FollowerSaleAuthentications a ON a.CustomerId = c.CustomerID
WHERE c.CustomerID = 53385;
""")
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
    Path(__file__).resolve().parent.parent.joinpath("_ops_tmp_reset_krar2_out.txt").write_text(
        out + ("\n" + err if err.strip() else ""), encoding="utf-8"
    )
    for line in out.splitlines():
        print(line.encode("ascii", errors="replace").decode("ascii"))
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
