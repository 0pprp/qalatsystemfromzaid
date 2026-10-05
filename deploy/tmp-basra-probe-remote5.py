#!/usr/bin/env python3
import json
import glob
import subprocess
import os

# Find all configs pointing at Basra DBs
hits = []
for p in glob.glob("/opt/qalaa-runtime/*/appsettings.json"):
    try:
        t = open(p, encoding="utf-8", errors="replace").read()
    except Exception:
        continue
    if "DatabaseCompanyBasra" in t and "Alanwar" not in t:
        hits.append(p)
print("CFG_HITS", hits)


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
    print("\n########", cfg_path, db)
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
        open("/tmp/basra_p6.sql", "w", encoding="utf-8").write(sql)
        r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_p6.sql"], capture_output=True, text=True)
        print("===", label, "RC", r.returncode, "===")
        print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:14000])

    run(
        """
SET NOCOUNT ON;
SELECT DelegateID, LTRIM(RTRIM(DelegateName)) N FROM dbo.Delegates
WHERE DelegateName LIKE N'%خمسة ميل الأولى%' OR DelegateName LIKE N'%خمسة ميل الاولى%';

SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest WHERE DelegateID=3;

SELECT COUNT(*) Cnt, CAST(ISNULL(SUM(Amount),0) AS bigint) SumAmt
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID IN (
  SELECT DelegateID FROM dbo.Delegates
  WHERE DelegateName LIKE N'%خمسة ميل الأولى%' OR DelegateName LIKE N'%خمسة ميل الاولى%'
);

-- Last 6 hours all pending
SELECT cpr.DelegateID, LTRIM(RTRIM(d.DelegateName)) N, COUNT(*) Cnt,
  CAST(SUM(cpr.Amount) AS bigint) SumAmt, COUNT(DISTINCT cpr.CustomerID) Custs,
  MIN(ISNULL(cpr.CreatedAtUtc,cpr.CreatedDate)) MinT,
  MAX(ISNULL(cpr.CreatedAtUtc,cpr.CreatedDate)) MaxT,
  SUM(CASE WHEN ISNULL(cpr.AutoPostEnabled,0)=1 THEN 1 ELSE 0 END) AutoCnt
FROM dbo.CustomersPaymentsRequest cpr
LEFT JOIN dbo.Delegates d ON d.DelegateID=cpr.DelegateID
WHERE ISNULL(cpr.CreatedAtUtc, cpr.CreatedDate) >= DATEADD(hour,-18,SYSUTCDATETIME())
   OR cpr.PaymentDate >= DATEADD(hour,-18,GETDATE())
GROUP BY cpr.DelegateID, LTRIM(RTRIM(d.DelegateName))
ORDER BY MaxT DESC;
""",
        "recent_pending",
    )

    run(
        """
SET NOCOUNT ON;
-- Find amount column on payments via views
SELECT TOP 20 TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Amount%' AND TABLE_NAME LIKE '%Payment%'
ORDER BY TABLE_NAME, COLUMN_NAME;

-- Search exact 379000 in request amounts recently
SELECT TOP 50 CustomersPaymentsRequestID, DelegateID, CustomerID,
  CAST(Amount AS bigint) Amount, PaymentDate, CreatedAtUtc, CreatedDate,
  ISNULL(AutoPostEnabled,0) AutoPost
FROM dbo.CustomersPaymentsRequest
WHERE CAST(Amount AS bigint)=379000
   OR CustomersPaymentsRequestID IN (
        SELECT TOP 1 CustomersPaymentsRequestID FROM dbo.CustomersPaymentsRequest ORDER BY 1 DESC
   );

-- Sum of last N requests that equal 379000 sliding window is hard; instead list groups by CreatedAtUtc timestamp
SELECT CONVERT(varchar(19), ISNULL(CreatedAtUtc,CreatedDate), 120) BatchT,
  DelegateID, COUNT(*) Cnt, CAST(SUM(Amount) AS bigint) SumAmt,
  COUNT(DISTINCT CustomerID) Custs
FROM dbo.CustomersPaymentsRequest
WHERE ISNULL(CreatedAtUtc,CreatedDate) >= DATEADD(day,-2,SYSUTCDATETIME())
GROUP BY CONVERT(varchar(19), ISNULL(CreatedAtUtc,CreatedDate), 120), DelegateID
HAVING CAST(SUM(Amount) AS bigint) BETWEEN 350000 AND 400000 OR COUNT(*)=15
ORDER BY BatchT DESC;
""",
        "batch_windows",
    )


for p in hits or ["/opt/qalaa-runtime/AdminBasra/appsettings.json"]:
    probe(p)

print("DONE")
