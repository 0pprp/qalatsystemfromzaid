import json
import subprocess

cfg = json.load(open("/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json", encoding="utf-8"))
cs = cfg["ConnectionStrings"]["DataBaseConnection"]
parts = {}
for p in cs.split(";"):
    if "=" in p:
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get("server") or parts.get("data source")
db = parts.get("initial catalog") or parts.get("database")
user = parts.get("user id") or parts.get("uid")
pwd = parts.get("password") or parts.get("pwd")

sql = r"""
SET NOCOUNT ON;

PRINT '=== ALL lists CP today ===';
SELECT d.DelegateID, d.DelegateName, COUNT(*) AS Cnt
FROM dbo.CustomersPayments cp
INNER JOIN dbo.Delegates d ON d.DelegateID = cp.DelegateID
WHERE CONVERT(date, cp.PaymentDate)='2026-09-21'
GROUP BY d.DelegateID, d.DelegateName
ORDER BY Cnt DESC;

PRINT '=== ALL Requests today ===';
SELECT d.DelegateID, d.DelegateName, COUNT(*) AS Cnt, SUM(r.Amount) AS Total,
  SUM(CASE WHEN r.PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPosted
FROM dbo.CustomersPaymentsRequest r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.DelegateID
WHERE CONVERT(date, r.PaymentDate)='2026-09-21'
   OR CONVERT(date, r.CreatedDate)='2026-09-21'
   OR CONVERT(date, r.ReceivedAtUtc)='2026-09-21'
GROUP BY d.DelegateID, d.DelegateName
ORDER BY Cnt DESC;

PRINT '=== Unposted requests ANY (last 2 days) ===';
SELECT TOP 40 r.CustomersPaymentsRequestID, r.DelegateID, d.DelegateName, r.CustomerID, r.Amount,
       r.PaymentDate, r.CreatedAtUtc, r.EligibleForPostingAtUtc, r.PostedToBoxAtUtc, r.ClientPaymentId, r.AutoPostEnabled
FROM dbo.CustomersPaymentsRequest r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.DelegateID
WHERE r.PostedToBoxAtUtc IS NULL
  AND r.CreatedDate >= DATEADD(day,-2, GETDATE())
ORDER BY r.CustomersPaymentsRequestID DESC;

PRINT '=== List5 payments with BoundNumber sum? ===';
SELECT TOP 5 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='View_CustomersPaymentsDelegate_Final' ORDER BY ORDINAL_POSITION;

IF OBJECT_ID('dbo.View_CustomersPaymentsDelegate_Final','V') IS NOT NULL
BEGIN
  SELECT COUNT(*) AS VCnt, SUM(CAST(Amount AS FLOAT)) AS VTotal
  FROM dbo.View_CustomersPaymentsDelegate_Final
  WHERE DelegateID=5 AND CONVERT(date, PaymentDate)='2026-09-21';
END

IF OBJECT_ID('dbo.View_CustomersPaymentsDelegate','V') IS NOT NULL
BEGIN
  SELECT TOP 20 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_NAME='View_CustomersPaymentsDelegate' ORDER BY ORDINAL_POSITION;
END

-- Temporary intake table (legacy sync?)
IF OBJECT_ID('dbo.SelectPaymentCustomerTemporary','U') IS NOT NULL
BEGIN
  SELECT TOP 5 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SelectPaymentCustomerTemporary';
  SELECT COUNT(*) AS TmpCnt FROM dbo.SelectPaymentCustomerTemporary WHERE DelegateID=5;
END

PRINT '=== HostedService / journal for post eligible ===';
"""
open("/tmp/paygap3.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd,
     "-i", "/tmp/paygap3.sql", "-W", "-s", "|", "-f", "65001"],
    capture_output=True, text=True,
)
print(r.stdout)
print(r.stderr[:500] if r.stderr else "")

# service logs
import subprocess as sp
print(sp.getoutput("systemctl list-units --type=service --all | grep -iE 'payment|eligible|delegate.*najaf' | head -20"))
print(sp.getoutput("journalctl -u qalaa-delegatehaidernajaflast -n 30 --no-pager 2>/dev/null | tail -30"))
