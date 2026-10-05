#!/usr/bin/env python3
"""Runs ON the Linux host — READ ONLY SQL against Basra DBs."""
import json
import os
import subprocess

CFGS = [
    "/opt/qalaa-runtime/AdminBasra/appsettings.json",
    "/opt/qalaa-runtime/AdminBasraAlanwar/appsettings.json",
]


def parse_cs(cs):
    parts = {}
    for part in cs.split(";"):
        if "=" in part:
            k, v = part.split("=", 1)
            parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("initial catalog") or parts.get("database"),
        parts.get("user id") or parts.get("uid"),
        parts.get("password") or parts.get("pwd"),
    )


def run(sqlcmd, sql, label):
    open("/tmp/basra_ro.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/basra_ro.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    out = r.stdout or ""
    if r.stderr:
        out += "\nSTDERR " + r.stderr[:1500]
    print(out[:14000])


for cfg_path in CFGS:
    if not os.path.isfile(cfg_path):
        print("SKIP missing", cfg_path)
        continue
    cfg = json.load(open(cfg_path, encoding="utf-8"))
    cs_map = cfg.get("ConnectionStrings") or {}
    cs = cs_map.get("DataBaseConnection") or cs_map.get("DefaultConnection")
    if not cs:
        print("SKIP no CS", cfg_path)
        continue
    server, db, user, pwd = parse_cs(cs)
    print("\n########", cfg_path, "DB=", db, "SERVER=", server, "########")
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

    run(
        sqlcmd,
        """SET NOCOUNT ON;
SELECT
  CASE WHEN OBJECT_ID(N'dbo.CustomersPaymentsRequest',N'U') IS NULL THEN 0 ELSE 1 END AS HasRequest,
  CASE WHEN OBJECT_ID(N'dbo.CustomersPayments',N'U') IS NULL THEN 0 ELSE 1 END AS HasPayments,
  CASE WHEN COL_LENGTH('dbo.CustomersPaymentsRequest','PostedToBoxAtUtc') IS NULL THEN 0 ELSE 1 END AS HasPostedCol,
  CASE WHEN COL_LENGTH('dbo.CustomersPaymentsRequest','ClientPaymentId') IS NULL THEN 0 ELSE 1 END AS HasClientId,
  CASE WHEN COL_LENGTH('dbo.CustomersPaymentsRequest','AutoPostEnabled') IS NULL THEN 0 ELSE 1 END AS HasAutoPost;
""",
        "schema",
    )

    run(
        sqlcmd,
        """SET NOCOUNT ON;
SELECT DelegateID, DelegateName, ISNULL(DelegateState,0) St
FROM dbo.Delegates
ORDER BY DelegateID;
""",
        "all_lists",
    )

    run(
        sqlcmd,
        """SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.CustomersPaymentsRequest',N'U') IS NULL
BEGIN PRINT 'NO_REQUEST_TABLE'; RETURN; END

DECLARE @Since DATETIME = DATEADD(day, -14, GETDATE());

SELECT d.DelegateID, d.DelegateName,
  COUNT(*) AS ReqCnt,
  CAST(ISNULL(SUM(cpr.Amount),0) AS bigint) AS ReqSum,
  SUM(CASE WHEN cpr.PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS UnpostedCnt,
  CAST(ISNULL(SUM(CASE WHEN cpr.PostedToBoxAtUtc IS NULL THEN cpr.Amount ELSE 0 END),0) AS bigint) AS UnpostedSum,
  SUM(CASE WHEN cpr.PostedToBoxAtUtc IS NOT NULL THEN 1 ELSE 0 END) AS PostedCnt,
  CAST(ISNULL(SUM(CASE WHEN cpr.PostedToBoxAtUtc IS NOT NULL THEN cpr.Amount ELSE 0 END),0) AS bigint) AS PostedSum,
  SUM(CASE WHEN ISNULL(cpr.AutoPostEnabled,0)=1 AND cpr.PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS AutoUnpostedCnt,
  MIN(ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate)) AS MinCreated,
  MAX(ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate)) AS MaxCreated
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) >= @Since
   OR cpr.PostedToBoxAtUtc IS NULL
GROUP BY d.DelegateID, d.DelegateName
ORDER BY UnpostedCnt DESC, ReqSum DESC;
""",
        "per_list_14d_and_unposted",
    )

    run(
        sqlcmd,
        """SET NOCOUNT ON;
IF COL_LENGTH('dbo.CustomersPaymentsRequest','PostedToBoxAtUtc') IS NULL
BEGIN PRINT 'NO_POSTED_COL'; RETURN; END

SELECT TOP 80
  d.DelegateID, d.DelegateName,
  cpr.CustomersPaymentsRequestID, c.CustomerID, c.CustomerName,
  CAST(cpr.Amount AS bigint) Amount,
  ISNULL(cpr.AutoPostEnabled,0) AutoPost,
  cpr.EligibleForPostingAtUtc,
  cpr.CreatedAtUtc, cpr.PaymentDate,
  LEFT(ISNULL(cpr.ClientPaymentId,''), 36) ClientPaymentId
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE cpr.PostedToBoxAtUtc IS NULL
ORDER BY ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) DESC, cpr.CustomersPaymentsRequestID DESC;
""",
        "unposted_detail_top80",
    )

    run(
        sqlcmd,
        """SET NOCOUNT ON;
DECLARE @BaghdadToday DATE = CAST(DATEADD(hour, 3, SYSUTCDATETIME()) AS date);
DECLARE @DayStartUtc DATETIME2 = DATEADD(hour, -3, CAST(@BaghdadToday AS DATETIME2));
DECLARE @DayEndUtc DATETIME2 = DATEADD(day, 1, @DayStartUtc);

PRINT CONCAT('BaghdadToday=', @BaghdadToday, ' DayStartUtc=', @DayStartUtc);

IF OBJECT_ID(N'dbo.CustomersPaymentsRequest',N'U') IS NOT NULL
BEGIN
  SELECT d.DelegateID, d.DelegateName,
    COUNT(*) Cnt, CAST(SUM(cpr.Amount) AS bigint) SumAmt,
    SUM(CASE WHEN cpr.PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) Unposted
  FROM dbo.CustomersPaymentsRequest cpr
  JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
  JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
  WHERE ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) >= @DayStartUtc
    AND ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate) < @DayEndUtc
  GROUP BY d.DelegateID, d.DelegateName
  ORDER BY SumAmt DESC;
END

IF OBJECT_ID(N'dbo.CustomersPayments',N'U') IS NOT NULL
BEGIN
  SELECT d.DelegateID, d.DelegateName,
    COUNT(*) Cnt, CAST(SUM(cp.Amount) AS bigint) SumAmt
  FROM dbo.CustomersPayments cp
  JOIN dbo.Customers c ON c.CustomerID = cp.CustomerID
  JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
  WHERE cp.DateCreate >= @DayStartUtc AND cp.DateCreate < @DayEndUtc
  GROUP BY d.DelegateID, d.DelegateName
  ORDER BY SumAmt DESC;
END
""",
        "today_baghdad",
    )

    run(
        sqlcmd,
        """SET NOCOUNT ON;
IF COL_LENGTH('dbo.CustomersPaymentsRequest','EligibleForPostingAtUtc') IS NULL
BEGIN PRINT 'NO_ELIG_COL'; RETURN; END

SELECT d.DelegateID, d.DelegateName,
  COUNT(*) StuckCnt,
  CAST(SUM(cpr.Amount) AS bigint) StuckSum,
  MIN(cpr.EligibleForPostingAtUtc) MinElig,
  MAX(cpr.EligibleForPostingAtUtc) MaxElig
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE cpr.PostedToBoxAtUtc IS NULL
  AND cpr.EligibleForPostingAtUtc IS NOT NULL
  AND cpr.EligibleForPostingAtUtc <= SYSUTCDATETIME()
GROUP BY d.DelegateID, d.DelegateName
HAVING COUNT(*) > 0
ORDER BY StuckCnt DESC;
""",
        "eligible_unposted_stuck",
    )

print("DONE_READONLY")
