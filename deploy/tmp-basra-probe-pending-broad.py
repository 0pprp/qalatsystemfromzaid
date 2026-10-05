#!/usr/bin/env python3
"""Broader probe: Basra خمسة ميل الأولى pending payment requests (any AutoPost)."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = r'''
import json, subprocess
cfg = json.load(open('/opt/qalaa-runtime/AdminBasra/appsettings.json', encoding='utf-8'))
cs = cfg['ConnectionStrings']['DataBaseConnection']
parts = {}
for part in cs.split(';'):
    if '=' in part:
        k,v = part.split('=',1); parts[k.strip().lower()] = v.strip()
server = parts.get('server') or parts.get('data source')
db = parts.get('initial catalog') or parts.get('database')
user = parts.get('user id') or parts.get('uid')
pwd = parts.get('password') or parts.get('pwd')
sqlcmd = ['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-h','-1']

def run(sql, label):
    open('/tmp/basra_p2.sql','w',encoding='utf-8').write(sql)
    r = subprocess.run(sqlcmd + ['-i','/tmp/basra_p2.sql'], capture_output=True, text=True)
    print('===', label, 'RC', r.returncode, '===')
    print(((r.stdout or '') + (('\nSTDERR '+r.stderr) if r.stderr else ''))[:12000])

# All pending requests for DelegateID=3
run('''SET NOCOUNT ON;
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);
PRINT CONCAT('DID=', @DID);

SELECT COUNT(*) AS AllPending, CAST(ISNULL(SUM(Amount),0) AS bigint) AS SumAmt
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID = @DID;

SELECT ISNULL(AutoPostEnabled,0) AS AutoPost, COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) Unposted
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID = @DID
GROUP BY ISNULL(AutoPostEnabled,0);

SELECT TOP 30 cpr.CustomersPaymentsRequestID, c.CustomerName,
  CAST(cpr.Amount AS bigint) Amount, ISNULL(cpr.AutoPostEnabled,0) AutoPost,
  cpr.PostedToBoxAtUtc, cpr.EligibleForPostingAtUtc, cpr.PaymentDate, cpr.CreatedAtUtc, cpr.ClientPaymentId
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID = @DID
ORDER BY cpr.CustomersPaymentsRequestID DESC;
''', 'pending_d3')

# Recent today-ish by amount clusters near 379000
run('''SET NOCOUNT ON;
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);

-- Groups of unposted by day
SELECT CONVERT(date, ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate)) AS D,
  COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) SumAmt,
  COUNT(DISTINCT c.CustomerID) Custs
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID = @DID
GROUP BY CONVERT(date, ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate))
ORDER BY D DESC;

-- Any pending totaling ~379k across any basra list today
SELECT d.DelegateID, d.DelegateName, COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) SumAmt,
  COUNT(DISTINCT c.CustomerID) Custs
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE cpr.PostedToBoxAtUtc IS NULL
  AND ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) >= DATEADD(day,-3, GETDATE())
GROUP BY d.DelegateID, d.DelegateName
HAVING COUNT(*) BETWEEN 10 AND 25 AND SUM(cpr.Amount) BETWEEN 300000 AND 450000
ORDER BY SumAmt DESC;
''', 'clusters')

# Also check view queue / non-auto
run('''SET NOCOUNT ON;
IF OBJECT_ID('dbo.View_CustomersPaymentsRequestFinal','V') IS NULL
BEGIN PRINT 'NO_VIEW'; RETURN; END
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);
SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt
FROM dbo.View_CustomersPaymentsRequestFinal
WHERE DelegateID = @DID;
SELECT TOP 20 CustomersPaymentsRequestID, CustomerName, CAST(Amount AS bigint) Amount, DelegateName
FROM dbo.View_CustomersPaymentsRequestFinal
WHERE DelegateID = @DID
ORDER BY CustomersPaymentsRequestID DESC;
''', 'view')

# Check if already in CustomersPayments today for this list ~379k
run('''SET NOCOUNT ON;
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);
SELECT CONVERT(date, cp.DateCreate) D, COUNT(*) Cnt,
  CAST(SUM(cp.Amount * 1448.0) AS bigint) ApproxIQD
FROM dbo.CustomersPayments cp
JOIN dbo.Customers c ON c.CustomerID = cp.CustomerID
WHERE c.DelegateID = @DID
  AND cp.DateCreate >= DATEADD(day,-2, GETDATE())
GROUP BY CONVERT(date, cp.DateCreate)
ORDER BY D DESC;
''', 'posted_recent')
'''

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/basra_p2.py", "w") as f:
                f.write(remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/basra_p2.py", timeout=120)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write((out + ("\nSTDERR " + err[:800] if err.strip() else "") + "\n").encode("utf-8", "replace"))
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
