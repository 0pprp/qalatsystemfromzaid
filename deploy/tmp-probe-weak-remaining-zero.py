#!/usr/bin/env python3
"""Probe WeakWeekPayers for remaining<=0 rows on Admin DBs."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import json, pathlib, subprocess
seen = set()
for apps in sorted(pathlib.Path('/opt/qalaa-runtime').glob('Admin*/appsettings.json')):
    name = apps.parent.name
    if 'backup' in name.lower() or name.endswith('-old'):
        continue
    try:
        cfg = json.loads(apps.read_text(encoding='utf-8', errors='replace'))
    except Exception:
        continue
    cs = (cfg.get('ConnectionStrings') or {}).get('DataBaseConnection') or ''
    parts = {}
    for part in cs.split(';'):
        if '=' in part:
            k, v = part.split('=', 1)
            parts[k.strip().lower()] = v.strip()
    server = parts.get('server') or parts.get('data source')
    db = parts.get('initial catalog') or parts.get('database')
    user = parts.get('user id') or parts.get('uid')
    pwd = parts.get('password') or parts.get('pwd')
    if not all([server, db, user, pwd]):
        continue
    key = (server, db)
    if key in seen:
        continue
    seen.add(key)

    def run(sql):
        r = subprocess.run(
            ['/opt/mssql-tools18/bin/sqlcmd', '-C', '-I', '-S', server, '-d', db,
             '-U', user, '-P', pwd, '-h', '-1', '-W', '-Q', sql],
            capture_output=True, text=True,
        )
        return (r.stdout or '').strip(), r.returncode

    defn, _ = run(
        "SET NOCOUNT ON; SELECT CASE "
        "WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.Customers_GetWeakWeekPayers')) "
        "LIKE N'%AmountTotalSales - ISNULL(P.ReceiptsTotal%' THEN 1 "
        "WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.Customers_GetWeakWeekPayers')) "
        "LIKE N'%AmountRemaining, 0) > 0%' THEN 2 ELSE 0 END;"
    )
    sql_cnt = (
        "SET NOCOUNT ON; "
        "IF OBJECT_ID('tempdb..#w') IS NOT NULL DROP TABLE #w; "
        "CREATE TABLE #w (CustomerID INT, AmountRemaining FLOAT); "
        "INSERT INTO #w (CustomerID, AmountRemaining) "
        "EXEC dbo.Customers_GetWeakWeekPayers; "
        "SELECT COUNT(*) AS Total, "
        "SUM(CASE WHEN ISNULL(AmountRemaining,0)<=0 THEN 1 ELSE 0 END) AS ZeroOrNeg "
        "FROM #w;"
    )
    # INSERT EXEC may fail if column list mismatches — fallback SELECT INTO via OPENQUERY not available.
    # Instead pull via simple SELECT into temp from result set using INSERT-EXEC with matching columns.
    sql_cnt2 = (
        "SET NOCOUNT ON; "
        "DECLARE @t TABLE (CustomerID INT, AmountRemaining FLOAT); "
        "INSERT INTO @t (CustomerID, AmountRemaining) "
        "SELECT CustomerID, AmountRemaining FROM ( "
        "  SELECT CustomerID, AmountRemaining FROM dbo.Customers_GetWeakWeekPayers "
        ") X; "
    )
    # SP cannot be used in FROM. Use INSERT-EXEC with full schema is heavy.
    # Check object text only + sample via sqlcmd -y with EXEC and count in python.
    out, rc = run("SET NOCOUNT ON; EXEC dbo.Customers_GetWeakWeekPayers;")
    lines = [ln for ln in out.splitlines() if ln.strip()]
    print(name, db, 'FILTER', defn.splitlines()[:1], 'EXEC_LINES', len(lines), 'RC', rc)
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_weak_z.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_weak_z.py", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
