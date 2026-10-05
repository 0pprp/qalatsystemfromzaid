#!/usr/bin/env python3
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
    "/opt/mssql-tools18/bin/sqlcmd",
    "-C",
    "-S",
    server,
    "-d",
    db,
    "-U",
    user,
    "-P",
    pwd,
    "-W",
    "-s",
    "|",
    "-h",
    "-1",
]


def run(sql: str, label: str) -> None:
    open("/tmp/basra_p4.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p4.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:14000])


run(
    """
SET NOCOUNT ON;
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);
PRINT CONCAT('DID=',@DID);

-- Today payments for list (Amount likely hash units)
SELECT CONVERT(date, cp.PaymentDate) D, COUNT(*) Cnt,
  CAST(SUM(cp.Amount) AS float) SumHash,
  CAST(SUM(cp.Amount)*1448.0 AS bigint) ApproxIQD
FROM dbo.CustomersPayments cp
JOIN dbo.Customers c ON c.CustomerID=cp.CustomerID
WHERE c.DelegateID=@DID AND cp.PaymentDate >= DATEADD(day,-3,CAST(GETDATE() AS date))
GROUP BY CONVERT(date, cp.PaymentDate)
ORDER BY D DESC;

-- Exact IQD ~379000 today any list
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt,
  CAST(SUM(cp.Amount)*1448.0 AS bigint) ApproxIQD,
  MIN(cp.PaymentDate) MinT, MAX(cp.PaymentDate) MaxT
FROM dbo.CustomersPayments cp
JOIN dbo.Customers c ON c.CustomerID=cp.CustomerID
JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
WHERE cp.PaymentDate >= DATEADD(day,-1,CAST(GETDATE() AS date))
GROUP BY d.DelegateID, LTRIM(RTRIM(d.DelegateName))
HAVING CAST(SUM(cp.Amount)*1448.0 AS bigint) BETWEEN 370000 AND 390000
   OR COUNT(*)=15
ORDER BY ApproxIQD DESC;

-- Pending requests where Amount sum 379000 with fuzzy list names
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID=cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
WHERE CAST(cpr.Amount AS bigint) > 0
GROUP BY d.DelegateID, LTRIM(RTRIM(d.DelegateName))
ORDER BY SumAmt DESC;
""",
    "payments_today",
)

run(
    """
SET NOCOUNT ON;
-- All pending with AutoPost=1 anywhere
SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest WHERE ISNULL(AutoPostEnabled,0)=1;

SELECT TOP 20 cpr.CustomersPaymentsRequestID, LTRIM(RTRIM(d.DelegateName)) N,
  c.CustomerName, CAST(cpr.Amount AS bigint) Amount,
  cpr.EligibleForPostingAtUtc, cpr.PostedToBoxAtUtc, cpr.CreatedAtUtc
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID=cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
WHERE ISNULL(cpr.AutoPostEnabled,0)=1
ORDER BY cpr.CustomersPaymentsRequestID DESC;

-- Search customer names partial for خمسة ميل list customers with recent requests via DelegateName alternate encodings
SELECT DelegateID, DelegateName, LEN(DelegateName) L, UNICODE(SUBSTRING(DelegateName,1,1)) U
FROM dbo.Delegates WHERE DelegateName LIKE N'%ميل%';
""",
    "autopost_and_names",
)

# Check if requests link via another column
run(
    """
SET NOCOUNT ON;
SELECT TOP 40 COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='CustomersPaymentsRequest' ORDER BY ORDINAL_POSITION;
""",
    "cpr_cols",
)

print("DONE")
