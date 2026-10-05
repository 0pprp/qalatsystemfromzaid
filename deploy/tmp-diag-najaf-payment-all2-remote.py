import json
import subprocess

cfg = json.load(open("/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json", encoding="utf-8"))
cs = cfg["ConnectionStrings"]["DataBaseConnection"]
parts = {}
for p in cs.split(";"):
    if "=" in p:
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get("server") or parts.get("data source")
db = parts.get("initial catalog") or parts.get("database")
user = parts.get("user id") or parts.get("uid")
pwd = parts.get("password") or parts.get("pwd")

sql = r"""
SET NOCOUNT ON;

PRINT '=== CP by list today ===';
SELECT TOP 30 d.DelegateID, d.DelegateName, COUNT(*) AS Cnt
FROM dbo.CustomersPayments cp
INNER JOIN dbo.Delegates d ON d.DelegateID = cp.DelegateID
WHERE CONVERT(date, cp.PaymentDate)='2026-09-21'
GROUP BY d.DelegateID, d.DelegateName
ORDER BY Cnt DESC;

PRINT '=== REQ by list today ===';
SELECT TOP 30 d.DelegateID, d.DelegateName, COUNT(*) AS Cnt, SUM(r.Amount) AS Total,
  SUM(CASE WHEN r.PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPosted
FROM dbo.CustomersPaymentsRequest r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.DelegateID
WHERE CONVERT(date, ISNULL(r.PaymentDate, r.CreatedDate))='2026-09-21'
   OR CONVERT(date, r.ReceivedAtUtc)='2026-09-21'
GROUP BY d.DelegateID, d.DelegateName
ORDER BY Cnt DESC;

PRINT '=== Unposted requests last 2 days ===';
SELECT TOP 50 r.CustomersPaymentsRequestID, r.DelegateID, d.DelegateName, r.CustomerID, r.Amount,
       r.PaymentDate, r.CreatedAtUtc, r.EligibleForPostingAtUtc, r.PostedToBoxAtUtc, r.ClientPaymentId
FROM dbo.CustomersPaymentsRequest r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.DelegateID
WHERE r.PostedToBoxAtUtc IS NULL
  AND r.CreatedDate >= DATEADD(day,-2, GETDATE())
ORDER BY r.CustomersPaymentsRequestID DESC;

PRINT '=== List5 request total ever today via CreatedAtUtc Baghdad ===';
SELECT COUNT(*) AS Cnt, SUM(Amount) AS Total
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=5
  AND CreatedAtUtc >= '2026-09-20 21:00:00' -- 2026-09-21 00:00 Baghdad
  AND CreatedAtUtc <  '2026-09-21 21:00:00';

PRINT '=== List5 CP ClientPaymentId filled today ===';
SELECT COUNT(*) AS WithClientId,
       SUM(CASE WHEN ClientPaymentId IS NULL OR LTRIM(RTRIM(ClientPaymentId))=N'' THEN 1 ELSE 0 END) AS LegacyNoClientId
FROM dbo.CustomersPayments
WHERE DelegateID=5 AND CONVERT(date, PaymentDate)='2026-09-21';

PRINT '=== Temporary table list5 ===';
IF OBJECT_ID('dbo.SelectPaymentCustomerTemporary','U') IS NOT NULL
BEGIN
  SELECT COUNT(*) AS TmpCnt FROM dbo.SelectPaymentCustomerTemporary WHERE DelegateID=5;
  SELECT TOP 20 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SelectPaymentCustomerTemporary' ORDER BY ORDINAL_POSITION;
END
"""
open("/tmp/paygap4.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd,
     "-i", "/tmp/paygap4.sql", "-W", "-s", "|", "-f", "65001"],
    capture_output=True, text=True,
)
open("/tmp/paygap4.out", "w", encoding="utf-8").write(r.stdout + "\n" + (r.stderr or ""))
print(r.stdout)
print("RC", r.returncode)
