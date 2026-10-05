#!/usr/bin/env python3
"""Verify week-offset fix applies to all Wilaya2 Oct2 payers, not one customer."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;

DECLARE @ListId INT = 5; -- الولاية نجف 2
DECLARE @Yday date = CONVERT(date, GETDATE()-1);

PRINT CONCAT('Yesterday=', @Yday);

-- All Wilaya2 customers who paid yesterday: how many have Amount2 matching?
;WITH Paid AS (
  SELECT p.CustomerID, SUM(ISNULL(a.Amount,0)*1448) AS PaidIqd
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Yday
  GROUP BY p.CustomerID
)
SELECT
  COUNT(*) AS PaidCustomers,
  SUM(CASE WHEN ISNULL(vc.Amount2,0) > 0 THEN 1 ELSE 0 END) AS Amount2Positive,
  SUM(CASE WHEN ABS(ISNULL(vc.Amount2,0) - p.PaidIqd) < 1 THEN 1 ELSE 0 END) AS Amount2MatchesPaid,
  SUM(CASE WHEN ISNULL(vc.Amount2,0) = 0 THEN 1 ELSE 0 END) AS Amount2StillZero
FROM Paid p
JOIN dbo.View_Customers vc ON vc.CustomerID=p.CustomerID;

PRINT '=== sample mismatches (if any) ===';
;WITH Paid AS (
  SELECT p.CustomerID, SUM(ISNULL(a.Amount,0)*1448) AS PaidIqd
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Yday
  GROUP BY p.CustomerID
)
SELECT TOP 20 c.CustomerName, p.PaidIqd, vc.Amount1 AS Today, vc.Amount2 AS Yesterday
FROM Paid p
JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
JOIN dbo.View_Customers vc ON vc.CustomerID=p.CustomerID
WHERE ABS(ISNULL(vc.Amount2,0) - p.PaidIqd) >= 1
ORDER BY c.CustomerName;

PRINT '=== Najaf-wide: any Oct2 payer with Amount2=0? ===';
;WITH Paid AS (
  SELECT p.CustomerID, SUM(ISNULL(a.Amount,0)*1448) AS PaidIqd
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE CONVERT(date,p.PaymentDate)=@Yday
  GROUP BY p.CustomerID
)
SELECT
  COUNT(*) AS AllNajafPaidYday,
  SUM(CASE WHEN ISNULL(vc.Amount2,0) > 0 THEN 1 ELSE 0 END) AS Amount2Ok,
  SUM(CASE WHEN ISNULL(vc.Amount2,0) = 0 THEN 1 ELSE 0 END) AS Amount2Zero
FROM Paid p
JOIN dbo.View_Customers vc ON vc.CustomerID=p.CustomerID;
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/verify_week_all.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/verify_week_all_runner.py", "w") as f:
        f.write(
            r"""
import json, os, subprocess
from pathlib import Path
cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
parts={}
for p in cfg['ConnectionStrings']['DataBaseConnection'].split(';'):
    if '=' in p:
        k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
bin='/opt/mssql-tools18/bin/sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','80','-s','|','-i','/tmp/verify_week_all.sql'], capture_output=True, text=True)
Path('/tmp/verify_week_all_out.txt').write_text((r.stdout or '')+'\n'+(r.stderr or ''), encoding='utf-8')
print('EXIT', r.returncode)
"""
        )
    sftp.close()
    _, out, _ = c.exec_command("python3 /tmp/verify_week_all_runner.py", timeout=180)
    print(out.read().decode("utf-8", "replace"))
    _, out2, _ = c.exec_command("cat /tmp/verify_week_all_out.txt", timeout=30)
    remote = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_verify_week_all_out.txt").write_text(remote, encoding="utf-8")
    for line in remote.splitlines():
        print(line.encode("ascii", "replace").decode("ascii"))
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
