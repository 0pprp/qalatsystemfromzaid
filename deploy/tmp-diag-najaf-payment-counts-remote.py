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
DECLARE @ListId INT = 5; -- الولاية نجف 2

PRINT '=== CustomersPayments 2026-09-21 ===';
SELECT COUNT(*) AS Cnt
FROM dbo.CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date, PaymentDate)='2026-09-21';

PRINT '=== CustomersPaymentsRequest same day ===';
SELECT COUNT(*) AS Cnt, SUM(CAST(ISNULL(Amount,0) AS FLOAT)) AS TotalAmount,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPosted,
  SUM(CASE WHEN PostedToBoxAtUtc IS NOT NULL THEN 1 ELSE 0 END) AS Posted,
  SUM(CASE WHEN EligibleForPostingAtUtc IS NOT NULL AND EligibleForPostingAtUtc > SYSUTCDATETIME() THEN 1 ELSE 0 END) AS NotYetEligible
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND (
    CONVERT(date, PaymentDate)='2026-09-21'
    OR CONVERT(date, CreatedDate)='2026-09-21'
    OR CONVERT(date, CreatedAtUtc)='2026-09-21'
  );

PRINT '=== Request rows not posted ===';
SELECT CustomersPaymentsRequestID, CustomerID, Amount, PaymentDate, CreatedDate,
       ClientPaymentId, CreatedAtUtc, EligibleForPostingAtUtc, PostedToBoxAtUtc, AutoPostEnabled, AsyncState
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND (
    CONVERT(date, PaymentDate)='2026-09-21'
    OR CONVERT(date, CreatedDate)='2026-09-21'
  )
  AND PostedToBoxAtUtc IS NULL
ORDER BY CustomersPaymentsRequestID;

PRINT '=== Request ALL today summary by posted ===';
SELECT CASE WHEN PostedToBoxAtUtc IS NULL THEN 'queue' ELSE 'posted' END AS Bucket,
       COUNT(*) AS Cnt, SUM(Amount) AS Total
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND (
    CONVERT(date, PaymentDate)='2026-09-21'
    OR CONVERT(date, CreatedDate)='2026-09-21'
  )
GROUP BY CASE WHEN PostedToBoxAtUtc IS NULL THEN 'queue' ELSE 'posted' END;

PRINT '=== View used by web? sample columns View_CustomersPayments ===';
SELECT TOP 3 * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='View_CustomersPaymentsDelegate' ORDER BY ORDINAL_POSITION;
SELECT TOP 3 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME LIKE 'View_CustomersPayments%' ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- Try catch-up SP now
PRINT '=== Run CustomersPaymentsRequest_PostEligible ===';
BEGIN TRY
  DECLARE @PostedCount INT;
  EXEC dbo.CustomersPaymentsRequest_PostEligible @UserCreateID=1, @PostedCount=@PostedCount OUTPUT;
  PRINT CONCAT('PostedCount=', ISNULL(CAST(@PostedCount AS nvarchar(20)),'NULL'));
END TRY
BEGIN CATCH
  PRINT CONCAT('SP_ERR=', ERROR_MESSAGE());
END CATCH;

PRINT '=== AFTER catch-up counts ===';
SELECT COUNT(*) AS CP_Cnt FROM dbo.CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date, PaymentDate)='2026-09-21';

SELECT COUNT(*) AS ReqCnt, SUM(Amount) AS Total,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPosted
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=@ListId
  AND (CONVERT(date, PaymentDate)='2026-09-21' OR CONVERT(date, CreatedDate)='2026-09-21');
"""
open("/tmp/paygap2.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd,
     "-i", "/tmp/paygap2.sql", "-W", "-s", "|", "-f", "65001"],
    capture_output=True, text=True,
)
print(r.stdout)
print(r.stderr[:800] if r.stderr else "")
print("RC", r.returncode)
