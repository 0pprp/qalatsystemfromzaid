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
    open("/tmp/basra_p8.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p8.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:16000])


run(
    """
SET NOCOUNT ON;
SELECT TOP 1 * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='View_CustomersPayments';
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='View_CustomersPayments' ORDER BY ORDINAL_POSITION;
""",
    "view_cols",
)

run(
    """
SET NOCOUNT ON;
-- Sep 20-21 for list 3 from view
SELECT CONVERT(date, PaymentDate) D, COUNT(*) Cnt, CAST(SUM(Amount) AS bigint) SumAmt,
  COUNT(DISTINCT CustomerID) Custs
FROM dbo.View_CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= '2026-09-19'
GROUP BY CONVERT(date, PaymentDate)
ORDER BY D DESC;

SELECT TOP 40 CustomerPaymentID, CustomerName, CAST(Amount AS bigint) Amount, PaymentDate, ClientPaymentId
FROM dbo.View_CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= '2026-09-20'
ORDER BY CustomerPaymentID DESC;
""",
    "view_list3",
)

run(
    """
SET NOCOUNT ON;
-- Batches yesterday evening ~379k for DID 3
SELECT CONVERT(varchar(16), PaymentDate, 120) Slot, COUNT(*) Cnt,
  CAST(SUM(Amount) AS bigint) SumAmt, COUNT(DISTINCT CustomerID) Custs
FROM dbo.View_CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= '2026-09-20'
GROUP BY CONVERT(varchar(16), PaymentDate, 120)
ORDER BY Slot DESC;

-- Any list with 15 payments totaling ~379k on Sep 20-21
SELECT DelegateID, COUNT(*) Cnt, CAST(SUM(Amount) AS bigint) SumAmt,
  COUNT(DISTINCT CustomerID) Custs, MIN(PaymentDate) MinT, MAX(PaymentDate) MaxT
FROM dbo.View_CustomersPayments
WHERE PaymentDate >= '2026-09-20'
GROUP BY DelegateID
HAVING COUNT(*)=15 OR CAST(SUM(Amount) AS bigint) BETWEEN 370000 AND 390000
ORDER BY SumAmt DESC;
""",
    "find_379_posted",
)

# Delegate API logs briefly
print("DONE")
