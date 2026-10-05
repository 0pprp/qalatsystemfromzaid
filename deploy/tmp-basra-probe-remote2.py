#!/usr/bin/env python3
import json
import subprocess

CFG_PATHS = [
    "/opt/qalaa-runtime/AdminBasra/appsettings.json",
    "/opt/qalaa-runtime/AdminBasraAlanwar/appsettings.json",
]


def probe(cfg_path: str) -> None:
    cfg = json.load(open(cfg_path, encoding="utf-8"))
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
    print("\n########", cfg_path, "DB=", db)
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
        open("/tmp/basra_p3.sql", "w", encoding="utf-8").write(sql)
        r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p3.sql"], capture_output=True, text=True)
        print("===", label, "RC", r.returncode, "===")
        print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:10000])

    run(
        """
SET NOCOUNT ON;
SELECT COUNT(*) PendingAll, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAll
FROM dbo.CustomersPaymentsRequest;

-- Exact 379000 clusters by delegate (pending)
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt, COUNT(DISTINCT c.CustomerID) Custs,
  MIN(ISNULL(cpr.CreatedAtUtc,cpr.PaymentDate)) MinT,
  MAX(ISNULL(cpr.CreatedAtUtc,cpr.PaymentDate)) MaxT
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID=cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
GROUP BY d.DelegateID, LTRIM(RTRIM(d.DelegateName))
HAVING CAST(SUM(cpr.Amount) AS bigint) BETWEEN 370000 AND 390000
    OR COUNT(*) = 15
ORDER BY SumAmt DESC;

-- Any خمسة ميل pending
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt, COUNT(DISTINCT c.CustomerID) Custs
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID=cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
WHERE d.DelegateName LIKE N'%خمسة ميل%'
GROUP BY d.DelegateID, LTRIM(RTRIM(d.DelegateName));

-- Latest 25 requests any list
SELECT TOP 25 cpr.CustomersPaymentsRequestID,
  LTRIM(RTRIM(d.DelegateName)) DelegateName, c.CustomerName,
  CAST(cpr.Amount AS bigint) Amount, ISNULL(cpr.AutoPostEnabled,0) AutoPost,
  ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) T
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID=cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID=c.DelegateID
ORDER BY cpr.CustomersPaymentsRequestID DESC;
""",
        "find_379",
    )

    run(
        """
SET NOCOUNT ON;
SELECT TOP 5 c.name FROM sys.columns c
JOIN sys.objects o ON o.object_id=c.object_id
WHERE o.name='CustomersPayments' ORDER BY c.column_id;

SELECT TOP 5 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='CustomersPayments';
""",
        "cols",
    )


for p in CFG_PATHS:
    try:
        open(p, encoding="utf-8").read(1)
    except FileNotFoundError:
        print("MISSING", p)
        continue
    probe(p)

print("DONE")
