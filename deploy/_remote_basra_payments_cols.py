#!/usr/bin/env python3
"""READ-ONLY column + count probe for Basra payment tables."""
import json
import os
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
    open("/tmp/basra_cols.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_cols.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:12000])


run(
    """SET NOCOUNT ON;
SELECT c.name, t.name AS Typ
FROM sys.columns c
JOIN sys.types t ON c.user_type_id=t.user_type_id
WHERE c.object_id=OBJECT_ID(N'dbo.CustomersPaymentsRequest')
ORDER BY c.column_id;
""",
    "cpr_cols",
)

run(
    """SET NOCOUNT ON;
SELECT COUNT(*) TotalRows FROM dbo.CustomersPaymentsRequest;
SELECT TOP 5 * FROM dbo.CustomersPaymentsRequest ORDER BY CustomersPaymentsRequestID DESC;
""",
    "cpr_sample",
)

run(
    """SET NOCOUNT ON;
SELECT c.name, t.name AS Typ
FROM sys.columns c
JOIN sys.types t ON c.user_type_id=t.user_type_id
WHERE c.object_id=OBJECT_ID(N'dbo.CustomersPayments')
ORDER BY c.column_id;
""",
    "cp_cols",
)

run(
    """SET NOCOUNT ON;
SELECT COUNT(*) TotalRows FROM dbo.CustomersPayments;
SELECT TOP 3 * FROM dbo.CustomersPayments ORDER BY 1 DESC;
""",
    "cp_sample",
)

run(
    """SET NOCOUNT ON;
-- Any unposted regardless of date
SELECT COUNT(*) UnpostedAll
FROM dbo.CustomersPaymentsRequest
WHERE PostedToBoxAtUtc IS NULL;

SELECT TOP 20 CustomersPaymentsRequestID, CustomerID, Amount, AutoPostEnabled,
  PostedToBoxAtUtc, EligibleForPostingAtUtc, CreatedAtUtc, PaymentDate, ClientPaymentId
FROM dbo.CustomersPaymentsRequest
WHERE PostedToBoxAtUtc IS NULL
ORDER BY CustomersPaymentsRequestID DESC;
""",
    "unposted_any",
)

run(
    """SET NOCOUNT ON;
SELECT TOP 15
  CONVERT(date, DATEADD(hour,3,ISNULL(CreatedAtUtc,PaymentDate))) AS BaghdadDay,
  COUNT(*) Cnt,
  CAST(SUM(Amount) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest
GROUP BY CONVERT(date, DATEADD(hour,3,ISNULL(CreatedAtUtc,PaymentDate)))
ORDER BY BaghdadDay DESC;
""",
    "cpr_by_day",
)

print("DONE")
