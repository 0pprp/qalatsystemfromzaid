#!/usr/bin/env python3
"""READ-ONLY: Basra CustomersPayments recent activity per list + amount source."""
import json
import subprocess

cfg = json.load(open("/opt/qalaa-runtime/AdminBasra/appsettings.json", encoding="utf-8"))
cs = cfg["ConnectionStrings"]["DataBaseConnection"]
parts = {}
for part in cs.split(";"):
    if "=" in part:
        k, v = part.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get("server") or parts.get("data source")
db = parts.get("initial catalog") or parts.get("database")
user = parts.get("user id") or parts.get("uid")
pwd = parts.get("password") or parts.get("pwd")
sqlcmd = [
    "/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd, "-W", "-s", "|", "-h", "-1",
]


def run(sql, label):
    open("/tmp/basra_act.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_act.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:14000])


run(
    """SET NOCOUNT ON;
-- Find amount-like columns / related tables
SELECT TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Amount%' OR COLUMN_NAME LIKE '%Payment%'
ORDER BY TABLE_NAME, COLUMN_NAME;
""",
    "amount_cols",
)

run(
    """SET NOCOUNT ON;
DECLARE @BaghdadToday DATE = CAST(DATEADD(hour, 3, SYSUTCDATETIME()) AS date);
DECLARE @DayStart DATETIME = DATEADD(hour, -3, CAST(@BaghdadToday AS DATETIME));
DECLARE @DayEnd DATETIME = DATEADD(day, 1, @DayStart);
PRINT CONCAT('BaghdadToday=', @BaghdadToday);

-- Today by CreatedDate (server receive time)
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) DelegateName,
  COUNT(*) Cnt
FROM dbo.CustomersPayments cp
JOIN dbo.Delegates d ON d.DelegateID = cp.DelegateID
WHERE cp.CreatedDate >= @DayStart AND cp.CreatedDate < @DayEnd
GROUP BY d.DelegateID, d.DelegateName
ORDER BY Cnt DESC;

PRINT '--- yesterday ---';
DECLARE @YDay DATE = DATEADD(day, -1, @BaghdadToday);
DECLARE @YStart DATETIME = DATEADD(hour, -3, CAST(@YDay AS DATETIME));
DECLARE @YEnd DATETIME = DATEADD(day, 1, @YStart);
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) DelegateName,
  COUNT(*) Cnt
FROM dbo.CustomersPayments cp
JOIN dbo.Delegates d ON d.DelegateID = cp.DelegateID
WHERE cp.CreatedDate >= @YStart AND cp.CreatedDate < @YEnd
GROUP BY d.DelegateID, d.DelegateName
ORDER BY Cnt DESC;
""",
    "payments_today_yesterday",
)

run(
    """SET NOCOUNT ON;
-- Last 7 Baghdad days totals
SELECT CONVERT(date, DATEADD(hour,3,cp.CreatedDate)) BaghdadDay,
  COUNT(*) Cnt,
  COUNT(DISTINCT cp.DelegateID) Lists,
  COUNT(DISTINCT cp.CustomerID) Customers
FROM dbo.CustomersPayments cp
WHERE cp.CreatedDate >= DATEADD(day, -8, GETDATE())
GROUP BY CONVERT(date, DATEADD(hour,3,cp.CreatedDate))
ORDER BY BaghdadDay DESC;
""",
    "last7_days",
)

run(
    """SET NOCOUNT ON;
-- Active field lists (State=1, typical collection) with payment counts last 3 days
DECLARE @Since DATETIME = DATEADD(day, -3, GETDATE());
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) Name,
  ISNULL(d.DelegateState,0) St,
  COUNT(cp.CustomerPaymentID) PayCnt3d,
  MAX(cp.CreatedDate) LastPayAt
FROM dbo.Delegates d
LEFT JOIN dbo.CustomersPayments cp
  ON cp.DelegateID = d.DelegateID AND cp.CreatedDate >= @Since
WHERE ISNULL(d.DelegateState,0)=1
  AND d.DelegateID BETWEEN 1 AND 22  -- classic Basra collection lists
GROUP BY d.DelegateID, d.DelegateName, d.DelegateState
ORDER BY d.DelegateID;
""",
    "classic_lists_3d",
)

run(
    """SET NOCOUNT ON;
-- Sample newest 15 payments with customer + list
SELECT TOP 15
  cp.CustomerPaymentID, cp.DelegateID, LTRIM(RTRIM(d.DelegateName)) ListName,
  cp.CustomerID, c.CustomerName, cp.BoundNumber, cp.PaymentDate, cp.CreatedDate,
  LEFT(ISNULL(cp.ClientPaymentId,''),36) ClientPaymentId
FROM dbo.CustomersPayments cp
JOIN dbo.Delegates d ON d.DelegateID = cp.DelegateID
LEFT JOIN dbo.Customers c ON c.CustomerID = cp.CustomerID
ORDER BY cp.CustomerPaymentID DESC;
""",
    "newest15",
)

print("DONE")
