import glob
import json
import os
import subprocess

def parse_cs(cs):
    parts = {}
    for p in (cs or "").split(";"):
        if "=" in p:
            k, v = p.split("=", 1)
            parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("initial catalog") or parts.get("database"),
        parts.get("user id") or parts.get("uid"),
        parts.get("password") or parts.get("pwd"),
    )

cands = []
for p in [
    "/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json",
    "/opt/qalaa-runtime/AdminNajaf/appsettings.json",
]:
    if os.path.isfile(p):
        cands.append(p)
cands += sorted(glob.glob("/opt/qalaa-runtime/Delegate*Najaf*/appsettings.json"))[:8]
print("CANDS", cands)

cfg_path = None
for p in cands:
    try:
        cfg = json.load(open(p, encoding="utf-8"))
        cs = cfg.get("ConnectionStrings", {}).get("DataBaseConnection")
        if cs:
            cfg_path = p
            break
    except Exception as e:
        print("skip", p, e)

print("USING", cfg_path)
cfg = json.load(open(cfg_path, encoding="utf-8"))
server, db, user, pwd = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
print("DB", server, db, user)

sql = r"""
SET NOCOUNT ON;

SELECT TOP 20 DelegateID, DelegateName, ReceiptName
FROM dbo.Delegates
WHERE DelegateName LIKE N'%ولاية%نجف%' OR DelegateName LIKE N'%نجف 2%'
ORDER BY DelegateID;

DECLARE @ListId INT = (
  SELECT TOP 1 DelegateID FROM dbo.Delegates
  WHERE DelegateName LIKE N'%ولاية%نجف%2%' OR DelegateName = N'الولاية نجف 2'
  ORDER BY DelegateID
);
PRINT CONCAT('LIST=', ISNULL(CAST(@ListId AS nvarchar(20)),'NULL'));

SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('CustomersPayments','CustomersPaymentsRequest','CustomersPaymentsRequests')
ORDER BY TABLE_NAME;

-- Posted box payments for calendar day 2026-09-21 Baghdad (DatePayment)
IF OBJECT_ID('dbo.CustomersPayments','U') IS NOT NULL
BEGIN
  SELECT 'CP_DatePayment' AS Src, COUNT(*) AS Cnt, SUM(CAST(ISNULL(Amount,0) AS FLOAT)) AS Total
  FROM dbo.CustomersPayments
  WHERE DelegateID=@ListId
    AND CONVERT(date, DatePayment) = '2026-09-21';

  SELECT 'CP_CreatedDate' AS Src, COUNT(*) AS Cnt, SUM(CAST(ISNULL(Amount,0) AS FLOAT)) AS Total
  FROM dbo.CustomersPayments
  WHERE DelegateID=@ListId
    AND CONVERT(date, CreatedDate) = '2026-09-21';
END

IF OBJECT_ID('dbo.CustomersPaymentsRequest','U') IS NOT NULL
BEGIN
  SELECT 'REQ_PaymentDate' AS Src, COUNT(*) AS Cnt, SUM(CAST(ISNULL(Amount,0) AS FLOAT)) AS Total,
         SUM(CASE WHEN ISNULL(AsyncState,0)=0 THEN 1 ELSE 0 END) AS PendingState0,
         SUM(CASE WHEN ISNULL(AsyncState,0)=1 THEN 1 ELSE 0 END) AS PostedState1
  FROM dbo.CustomersPaymentsRequest
  WHERE DelegateID=@ListId
    AND (
      CONVERT(date, PaymentDate) = '2026-09-21'
      OR CONVERT(date, CreatedAtUtc AT TIME ZONE 'UTC' AT TIME ZONE 'Arabic Standard Time') = '2026-09-21'
      OR CONVERT(date, CreatedDate) = '2026-09-21'
    );

  SELECT TOP 50 CustomersPaymentsRequestID, CustomerID, Amount, PaymentDate, CreatedDate,
         ClientPaymentId, CreatedAtUtc, EligibleForPostingAtUtc, AutoPostEnabled, AsyncState, AsyncID
  FROM dbo.CustomersPaymentsRequest
  WHERE DelegateID=@ListId
    AND (
      CONVERT(date, PaymentDate) = '2026-09-21'
      OR CONVERT(date, CreatedDate) = '2026-09-21'
    )
  ORDER BY CustomersPaymentsRequestID DESC;
END
"""
open("/tmp/paygap.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    [
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
        "-i",
        "/tmp/paygap.sql",
        "-W",
        "-s",
        "|",
        "-f",
        "65001",
    ],
    capture_output=True,
    text=True,
)
print(r.stdout)
print(r.stderr[:800] if r.stderr else "")
print("RC", r.returncode)
