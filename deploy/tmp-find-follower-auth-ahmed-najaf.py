#!/usr/bin/env python3
"""Broader lookup of recent FollowerSaleAuthentications in Najaf for Ahmed / كرار."""
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

cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(sql: str):
    # Force UTF-8 results via code page; also emit CustomerId-focused ASCII lines
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    print(p.stdout or '')
    if p.stderr:
        print(p.stderr)
    return p.returncode

print('DB', db)
print('=== table exists? ===')
run("SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerSaleAuthentications',N'U') IS NULL THEN 0 ELSE 1 END AS HasTable; SELECT COUNT(*) AS AuthCount FROM dbo.FollowerSaleAuthentications;")

print('=== lists matching شمالي ===')
run("""
SELECT DelegateID, DelegateName FROM dbo.Delegates
WHERE DelegateName LIKE N'%شمالي%' OR DelegateName LIKE N'%شمال%';
""")

print('=== customers كرار/فليح ===')
run("""
SELECT c.CustomerID, c.DelegateID, d.DelegateName,
  CONVERT(VARCHAR(200), c.CustomerName) AS NameAscii,
  LEN(c.CustomerName) AS NameLen
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE c.CustomerName LIKE N'%كرار%' OR c.CustomerName LIKE N'%فليح%' OR c.CustomerName LIKE N'%اركان%'
ORDER BY c.CustomerID DESC;
""")

print('=== recent auths (top 30) ===')
run("""
SELECT TOP 30
  a.Id, a.CustomerId, a.ListId, a.Rating, a.FollowerUserId,
  CONVERT(VARCHAR(80), a.FollowerName) AS FollowerAscii,
  CONVERT(VARCHAR(120), c.CustomerName) AS CustomerAscii,
  CONVERT(VARCHAR(80), d.DelegateName) AS ListAscii,
  a.AuthenticatedAtUtc
FROM dbo.FollowerSaleAuthentications a
LEFT JOIN dbo.Customers c ON c.CustomerID = a.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID = a.ListId
ORDER BY a.AuthenticatedAtUtc DESC;
""")

print('=== auths by follower احمد ===')
run("""
SELECT a.Id, a.CustomerId, a.ListId, a.Rating, a.FollowerUserId,
  CONVERT(VARCHAR(80), a.FollowerName) AS FollowerAscii,
  CONVERT(VARCHAR(120), c.CustomerName) AS CustomerAscii,
  a.AuthenticatedAtUtc
FROM dbo.FollowerSaleAuthentications a
LEFT JOIN dbo.Customers c ON c.CustomerID = a.CustomerId
WHERE a.FollowerName LIKE N'%احمد%' OR a.FollowerName LIKE N'%أحمد%'
ORDER BY a.AuthenticatedAtUtc DESC;
""")

print('=== auth for customer 53385 any list ===')
run("SELECT * FROM dbo.FollowerSaleAuthentications WHERE CustomerId = 53385;")

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
    path = Path(__file__).resolve().parent.parent / "_ops_tmp_find_auth_ahmed_out.txt"
    path.write_text(out + ("\n" + err if err.strip() else ""), encoding="utf-8")
    for line in out.splitlines():
        print(line.encode("ascii", errors="replace").decode("ascii"))
    print(f"FULL_LOG {path}")
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
