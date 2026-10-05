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
    open("/tmp/basra_p5.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p5.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:16000])


run(
    """
SET NOCOUNT ON;
DECLARE @DID INT = 3;
PRINT CONCAT('DID=',@DID,' NAME=', (SELECT LTRIM(RTRIM(DelegateName)) FROM Delegates WHERE DelegateID=@DID));

SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt,
  COUNT(DISTINCT CustomerID) Custs,
  SUM(CASE WHEN ISNULL(AutoPostEnabled,0)=1 THEN 1 ELSE 0 END) AutoCnt,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) Unposted
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@DID;

SELECT ISNULL(AutoPostEnabled,0) AutoPost, COUNT(*) Cnt, CAST(SUM(Amount) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@DID
GROUP BY ISNULL(AutoPostEnabled,0);

SELECT TOP 40 CustomersPaymentsRequestID, CustomerID, CAST(Amount AS bigint) Amount,
  ISNULL(AutoPostEnabled,0) AutoPost, PostedToBoxAtUtc, EligibleForPostingAtUtc,
  PaymentDate, CreatedAtUtc, ClientPaymentId, ReceiptNumber
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@DID
ORDER BY CustomersPaymentsRequestID DESC;
""",
    "by_cpr_delegate",
)

run(
    """
SET NOCOUNT ON;
-- Match 15 / ~379000 by cpr.DelegateID
SELECT cpr.DelegateID, LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt, COUNT(DISTINCT cpr.CustomerID) Custs,
  SUM(CASE WHEN ISNULL(cpr.AutoPostEnabled,0)=1 THEN 1 ELSE 0 END) AutoCnt
FROM dbo.CustomersPaymentsRequest cpr
LEFT JOIN dbo.Delegates d ON d.DelegateID=cpr.DelegateID
GROUP BY cpr.DelegateID, LTRIM(RTRIM(d.DelegateName))
HAVING COUNT(*) BETWEEN 12 AND 20 OR CAST(SUM(cpr.Amount) AS bigint) BETWEEN 370000 AND 390000
ORDER BY SumAmt DESC;

-- Distinct customers for DID=3
SELECT c.CustomerName, COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID=cpr.CustomerID
WHERE cpr.DelegateID=3
GROUP BY c.CustomerName
ORDER BY Cnt DESC;
""",
    "match_and_names",
)

run(
    """
SET NOCOUNT ON;
SELECT TOP 30 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='CustomersPayments' ORDER BY ORDINAL_POSITION;
""",
    "cp_cols",
)

print("DONE")
