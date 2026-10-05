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
    open("/tmp/basra_p9.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p9.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:16000])


run(
    """
SET NOCOUNT ON;
SELECT CONVERT(date, PaymentDate) D, COUNT(*) Cnt, CAST(SUM(AmountDenar) AS bigint) SumAmt,
  COUNT(DISTINCT CustomerID) Custs
FROM dbo.View_CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= '2026-09-19'
GROUP BY CONVERT(date, PaymentDate)
ORDER BY D DESC;

SELECT TOP 40 CustomerPaymentID, CustomerName, CAST(AmountDenar AS bigint) Amount, PaymentDate
FROM dbo.View_CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= '2026-09-20'
ORDER BY CustomerPaymentID DESC;
""",
    "list3_posted",
)

run(
    """
SET NOCOUNT ON;
-- Find 15 / ~379k posted recently any list
SELECT DelegateID, LTRIM(RTRIM(MAX(DelegateName))) N, COUNT(*) Cnt,
  CAST(SUM(AmountDenar) AS bigint) SumAmt, COUNT(DISTINCT CustomerID) Custs,
  MIN(PaymentDate) MinT, MAX(PaymentDate) MaxT
FROM dbo.View_CustomersPayments
WHERE PaymentDate >= '2026-09-20'
GROUP BY DelegateID
HAVING COUNT(*) BETWEEN 14 AND 16 OR CAST(SUM(AmountDenar) AS bigint) BETWEEN 370000 AND 390000
ORDER BY MaxT DESC, SumAmt DESC;

-- Slot batches for DID=3
SELECT CONVERT(varchar(16), PaymentDate, 120) Slot, COUNT(*) Cnt,
  CAST(SUM(AmountDenar) AS bigint) SumAmt, COUNT(DISTINCT CustomerID) Custs
FROM dbo.View_CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= '2026-09-20'
GROUP BY CONVERT(varchar(16), PaymentDate, 120)
HAVING COUNT(*) >= 5
ORDER BY Slot DESC;
""",
    "find_batches",
)

run(
    """
SET NOCOUNT ON;
-- Pending approval queue total
SELECT COUNT(*) Pending, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt FROM dbo.CustomersPaymentsRequest;
SELECT TOP 5 LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) S
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Delegates d ON d.DelegateID=cpr.DelegateID
GROUP BY LTRIM(RTRIM(d.DelegateName))
ORDER BY S DESC;
""",
    "queue",
)

print("DONE")
