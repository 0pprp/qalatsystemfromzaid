#!/usr/bin/env python3
import json
import subprocess
import sys

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
    open("/tmp/basra_p2.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p2.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    out = (r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else "")
    print(out[:12000])


run(
    """
SET NOCOUNT ON;
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

SELECT TOP 40 cpr.CustomersPaymentsRequestID, c.CustomerName,
  CAST(cpr.Amount AS bigint) Amount, ISNULL(cpr.AutoPostEnabled,0) AutoPost,
  cpr.PostedToBoxAtUtc, cpr.EligibleForPostingAtUtc, cpr.PaymentDate, cpr.CreatedAtUtc
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID = @DID
ORDER BY cpr.CustomersPaymentsRequestID DESC;
""",
    "pending_d3",
)

run(
    """
SET NOCOUNT ON;
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);

SELECT CONVERT(date, ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate)) AS D,
  COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) SumAmt,
  COUNT(DISTINCT c.CustomerID) Custs
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID = @DID
GROUP BY CONVERT(date, ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate))
ORDER BY D DESC;

SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) AS DelegateName, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt, COUNT(DISTINCT c.CustomerID) Custs
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE cpr.PostedToBoxAtUtc IS NULL
  AND ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) >= DATEADD(day,-3, GETDATE())
GROUP BY d.DelegateID, LTRIM(RTRIM(d.DelegateName))
HAVING COUNT(*) BETWEEN 10 AND 25 AND SUM(cpr.Amount) BETWEEN 300000 AND 450000
ORDER BY SumAmt DESC;

SELECT TOP 30 d.DelegateID, LTRIM(RTRIM(d.DelegateName)) AS DelegateName, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) >= DATEADD(day,-1, GETDATE())
GROUP BY d.DelegateID, LTRIM(RTRIM(d.DelegateName))
ORDER BY Cnt DESC;
""",
    "clusters",
)

run(
    """
SET NOCOUNT ON;
DECLARE @DID INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE LTRIM(RTRIM(DelegateName)) = N'خمسة ميل الأولى'
);
IF OBJECT_ID('dbo.View_CustomersPaymentsRequestFinal','V') IS NULL
BEGIN PRINT 'NO_VIEW'; END
ELSE
BEGIN
  SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt
  FROM dbo.View_CustomersPaymentsRequestFinal WHERE DelegateID = @DID;
  SELECT TOP 20 CustomersPaymentsRequestID, CustomerName, CAST(Amount AS bigint) Amount
  FROM dbo.View_CustomersPaymentsRequestFinal WHERE DelegateID = @DID
  ORDER BY CustomersPaymentsRequestID DESC;
END

SELECT CONVERT(date, cp.DateCreate) D, COUNT(*) Cnt,
  CAST(SUM(cp.Amount * 1448.0) AS bigint) ApproxIQD
FROM dbo.CustomersPayments cp
JOIN dbo.Customers c ON c.CustomerID = cp.CustomerID
WHERE c.DelegateID = @DID AND cp.DateCreate >= DATEADD(day,-2, GETDATE())
GROUP BY CONVERT(date, cp.DateCreate)
ORDER BY D DESC;
""",
    "view_and_posted",
)

print("DONE")
