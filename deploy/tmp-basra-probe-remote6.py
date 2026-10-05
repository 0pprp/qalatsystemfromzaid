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
    open("/tmp/basra_p7.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p7.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:16000])


run(
    """
SET NOCOUNT ON;
-- Temporary payments
IF OBJECT_ID('dbo.SelectPaymentCustomerTemporary','U') IS NOT NULL
BEGIN
  SELECT TOP 5 * FROM dbo.SelectPaymentCustomerTemporary;
  SELECT DelegateID, COUNT(*) Cnt, CAST(SUM(Amount) AS bigint) SumAmt
  FROM dbo.SelectPaymentCustomerTemporary
  GROUP BY DelegateID
  HAVING COUNT(*)>=10 OR SUM(Amount) BETWEEN 350000 AND 400000;
END
ELSE PRINT 'NO_TEMP';

-- Balance requests
IF OBJECT_ID('dbo.CustomerPaymentBalanceRequest','U') IS NOT NULL
BEGIN
  SELECT COUNT(*) Cnt FROM dbo.CustomerPaymentBalanceRequest;
  SELECT TOP 20 * FROM dbo.CustomerPaymentBalanceRequest ORDER BY 1 DESC;
END
""",
    "temp_tables",
)

run(
    """
SET NOCOUNT ON;
-- Discover amount field for CustomersPayments
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='CustomersPayments';

-- Views with payment amounts
SELECT TOP 5 name FROM sys.views WHERE name LIKE '%Payment%';

IF OBJECT_ID('dbo.View_CustomersPayments','V') IS NOT NULL
  SELECT TOP 3 * FROM dbo.View_CustomersPayments;
IF OBJECT_ID('dbo.View_CustomersPaymentFinal','V') IS NOT NULL
  SELECT TOP 3 * FROM dbo.View_CustomersPaymentFinal;
""",
    "payment_schema",
)

run(
    """
SET NOCOUNT ON;
-- Recent CPR deleted? Check max id and identity
SELECT MAX(CustomersPaymentsRequestID) MaxId, COUNT(*) StillPending
FROM dbo.CustomersPaymentsRequest;

-- Any CPR for DID=3 in last id range that might have been posted - check CustomersPayments by ClientPaymentId pattern today
SELECT COUNT(*) CpToday
FROM dbo.CustomersPayments
WHERE PaymentDate >= CAST(GETDATE() AS date) AND DelegateID=3;

SELECT TOP 30 CustomerPaymentID, CustomerID, DelegateID, PaymentDate, ClientPaymentId, BoundNumber, Location
FROM dbo.CustomersPayments
WHERE DelegateID=3 AND PaymentDate >= DATEADD(day,-2,CAST(GETDATE() AS date))
ORDER BY CustomerPaymentID DESC;

-- Count today by delegate
SELECT DelegateID, COUNT(*) Cnt
FROM dbo.CustomersPayments
WHERE PaymentDate >= CAST(GETDATE() AS date)
GROUP BY DelegateID
ORDER BY Cnt DESC;
""",
    "already_posted",
)

run(
    """
SET NOCOUNT ON;
-- Maybe amount lives in BoundNumber weirdly? Or look at Approve queue view for DID 3
IF OBJECT_ID('dbo.View_CustomersPaymentsRequestFinal','V') IS NOT NULL
BEGIN
  SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt
  FROM dbo.View_CustomersPaymentsRequestFinal WHERE DelegateID=3;
  SELECT TOP 20 CustomersPaymentsRequestID, CustomerName, CAST(Amount AS bigint) Amount, DelegateName
  FROM dbo.View_CustomersPaymentsRequestFinal WHERE DelegateID=3;
END

-- Search 379000 as sum of ANY open requests with 2 customers
SELECT cpr.DelegateID, COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) SumAmt,
  COUNT(DISTINCT cpr.CustomerID) Custs
FROM dbo.CustomersPaymentsRequest cpr
GROUP BY cpr.DelegateID
HAVING COUNT(DISTINCT cpr.CustomerID)=2;
""",
    "two_customers",
)

print("DONE")
