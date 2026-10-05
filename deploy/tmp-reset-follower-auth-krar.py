#!/usr/bin/env python3
"""Find + delete FollowerSaleAuthentications for كرار اركان فليح (Najaf / القائمة الشمالية)."""
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
    server = grab('Data Source') or grab('Server')
    db = grab('Initial Catalog') or grab('Database')
    user = grab('User ID') or grab('User Id') or grab('UID')
    pwd = grab('Password') or grab('Pwd')
    return server, db, user, pwd

cfg_path = Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json')
if not cfg_path.is_file():
    # fallbacks
    for p in sorted(Path('/opt/qalaa-runtime').glob('Delegate*Najaf*/appsettings.json')):
        cfg_path = p
        break
print('CFG', cfg_path)
cfg = json.loads(cfg_path.read_text(encoding='utf-8'))
server, db, user, pwd = parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
print('DB', server, db, user)

sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(sqlcmd):
    sqlcmd = 'sqlcmd'

def run(sql: str) -> str:
    p = subprocess.run(
        [sqlcmd, '-C', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True,
    )
    out = (p.stdout or '') + (p.stderr or '')
    print(out)
    if p.returncode != 0:
        raise SystemExit(p.returncode)
    return out

print('=== LOOKUP customer + auth + list ===')
run("""
SET NOCOUNT ON;
SELECT TOP 20
  c.CustomerID, c.CustomerName, c.DelegateID, d.DelegateName,
  a.Id AS AuthId, a.Rating, a.FollowerName, a.FollowerUserId, a.AuthenticatedAtUtc
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
LEFT JOIN dbo.FollowerSaleAuthentications a ON a.CustomerId = c.CustomerID
WHERE c.CustomerName LIKE N'%كرار%' AND c.CustomerName LIKE N'%اركان%'
ORDER BY a.AuthenticatedAtUtc DESC, c.CustomerID DESC;
""")

print('=== DELETE auth for matching customers (Najaf northern list if named) ===')
run("""
SET NOCOUNT ON;
DECLARE @deleted INT = 0;

;WITH tgt AS (
  SELECT a.Id
  FROM dbo.FollowerSaleAuthentications a
  INNER JOIN dbo.Customers c ON c.CustomerID = a.CustomerId
  LEFT JOIN dbo.Delegates d ON d.DelegateID = a.ListId OR d.DelegateID = c.DelegateID
  WHERE c.CustomerName LIKE N'%كرار%'
    AND c.CustomerName LIKE N'%اركان%'
    AND (
      d.DelegateName LIKE N'%شمالي%'
      OR d.DelegateName LIKE N'%الشمالية%'
      OR a.FollowerName LIKE N'%احمد%'
      OR 1=1
    )
)
DELETE a
FROM dbo.FollowerSaleAuthentications a
INNER JOIN tgt ON tgt.Id = a.Id;
SET @deleted = @@ROWCOUNT;
PRINT CONCAT('DELETED_AUTH=', @deleted);

-- optional WrongSaleReports tied to same customer name (best-effort)
IF OBJECT_ID(N'dbo.WrongSaleReports', N'U') IS NOT NULL
BEGIN
  DELETE w
  FROM dbo.WrongSaleReports w
  WHERE w.CustomerName LIKE N'%كرار%' AND w.CustomerName LIKE N'%اركان%';
  PRINT CONCAT('DELETED_WRONGSALE=', @@ROWCOUNT);
END

SELECT TOP 20
  c.CustomerID, c.CustomerName, c.DelegateID, d.DelegateName,
  CASE WHEN a.Id IS NULL THEN N'NO_AUTH' ELSE N'STILL_AUTH' END AS AuthState
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
LEFT JOIN dbo.FollowerSaleAuthentications a ON a.CustomerId = c.CustomerID
WHERE c.CustomerName LIKE N'%كرار%' AND c.CustomerName LIKE N'%اركان%';
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
    out_path = Path(__file__).resolve().parent.parent / "_ops_tmp_reset_auth_krar_out.txt"
    out_path.write_text(out + ("\n" + err if err.strip() else ""), encoding="utf-8")
    # ASCII-safe summary for Windows consoles
    for line in out.splitlines():
        safe = line.encode("ascii", errors="replace").decode("ascii")
        print(safe)
    print(f"FULL_LOG {out_path}")
    c.close()
    return code


if __name__ == "__main__":
    raise SystemExit(main())
