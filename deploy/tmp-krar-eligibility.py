#!/usr/bin/env python3
"""Eligibility of customer 53385 for pending sales + Ahmed follower linkage."""
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
cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])

def run(sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    print(p.stdout or ''); 
    if p.stderr: print(p.stderr)
    return p.returncode

print('DB', db)
print('=== sales + drafts 53385 ===')
run("""
SET NOCOUNT ON;
SELECT TOP 5 CustomerSaleID, DateCreate FROM dbo.CustomersSales WHERE CustomerID=53385 ORDER BY DateCreate DESC;
SELECT TOP 5 SaleId, Status, PostingStatus, PostedAtUtc FROM dbo.SalesDrafts WHERE CustomerId=53385 ORDER BY SaleId DESC;
SELECT COUNT(*) AS Auth FROM dbo.FollowerSaleAuthentications WHERE CustomerId=53385;
SELECT DelegateID, CONVERT(VARCHAR(80), DelegateName) AS N FROM dbo.Delegates WHERE DelegateID=1;
""")

print('=== followers linked to list 1 + name احمد ===')
run("""
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.FollowerUserLists',N'U') IS NOT NULL
BEGIN
  SELECT ful.UserId, ful.ListId,
    CONVERT(VARCHAR(80), ISNULL(u.FullName, u.UserName)) AS UserA
  FROM dbo.FollowerUserLists ful
  LEFT JOIN dbo.Users u ON u.UserId = ful.UserId
  WHERE ful.ListId = 1;
END
ELSE PRINT 'no FollowerUserLists';

SELECT TOP 30 UserId,
  CONVERT(VARCHAR(80), ISNULL(FullName, UserName)) AS UserA,
  IsFollower, IsActive
FROM dbo.Users
WHERE FullName LIKE N'%احمد%' OR FullName LIKE N'%أحمد%' OR UserName LIKE N'%ahmad%' OR UserName LIKE N'%ahmed%'
ORDER BY UserId DESC;
""")

print('=== pending-query match for 53385 ===')
run("""
SET NOCOUNT ON;
SELECT
  c.CustomerID,
  CASE WHEN sd.SaleId IS NULL THEN 0 ELSE 1 END AS HasPostedDraft,
  CASE WHEN a.Id IS NULL THEN 0 ELSE 1 END AS HasAuth,
  cs.CustomerSaleID,
  sd.SaleId
FROM dbo.Customers c
OUTER APPLY (
  SELECT TOP 1 s.CustomerSaleID, s.DateCreate
  FROM dbo.CustomersSales s WHERE s.CustomerID=c.CustomerID
  ORDER BY s.DateCreate DESC, s.CustomerSaleID DESC
) cs
OUTER APPLY (
  SELECT TOP 1 sd.SaleId
  FROM dbo.SalesDrafts sd
  WHERE sd.CustomerId=c.CustomerID AND sd.Status=N'Completed' AND ISNULL(sd.PostingStatus,N'')=N'Posted'
  ORDER BY sd.PostedAtUtc DESC, sd.SaleId DESC
) sd
LEFT JOIN dbo.FollowerSaleAuthentications a ON a.CustomerId=c.CustomerID AND a.ListId=c.DelegateID
WHERE c.CustomerID=53385;
""")

# Also scan ALL branch DBs for auth on 53385 or name
print('=== scan all Delegate* DBs for auth CustomerId 53385 or recent auth ===')
for p in sorted(Path('/opt/qalaa-runtime').glob('Delegate*/appsettings.json')):
    try:
        c2 = json.loads(p.read_text(encoding='utf-8'))
        s2,d2,u2,p2 = parse_cs(c2['ConnectionStrings']['DataBaseConnection'])
    except Exception:
        continue
    q = """
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.FollowerSaleAuthentications',N'U') IS NULL
BEGIN SELECT 'NO_TABLE' AS X; RETURN; END
SELECT COUNT(*) AS Cnt FROM dbo.FollowerSaleAuthentications;
SELECT TOP 3 Id, CustomerId, Rating, AuthenticatedAtUtc FROM dbo.FollowerSaleAuthentications ORDER BY AuthenticatedAtUtc DESC;
SELECT Id, CustomerId, Rating FROM dbo.FollowerSaleAuthentications WHERE CustomerId=53385;
"""
    proc = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', s2, '-d', d2, '-U', u2, '-P', p2, '-W', '-s', '|', '-Q', q],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    body = (proc.stdout or '')
    if '53385' in body or ( 'Cnt' in body and not re.search(r'Cnt\s*\n-+\s*\n0\s*$', body, re.M) ):
        # print if has any auths or mentions 53385
        if '53385' in body or re.search(r'\n([1-9]\d*)\s*$', body.split('Cnt')[-1][:80] if 'Cnt' in body else ''):
            print('BRANCH', p.parent.name, 'DB', d2)
            print(body[:800])

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
    path = Path(__file__).resolve().parent.parent / "_ops_tmp_krar_elig_out.txt"
    path.write_text(out + ("\n" + err if err.strip() else ""), encoding="utf-8")
    for line in out.splitlines():
        print(line.encode("ascii", errors="replace").decode("ascii"))
    print("FULL_LOG", path)
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
