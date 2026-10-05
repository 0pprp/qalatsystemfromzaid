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
SET XACT_ABORT ON;

-- Legacy intake rows never got EligibleForPostingAtUtc → SP skips them forever.
UPDATE dbo.CustomersPaymentsRequest
SET AutoPostEnabled = 1,
    CreatedAtUtc = ISNULL(CreatedAtUtc, CASE
        WHEN PaymentDate IS NOT NULL THEN CAST(PaymentDate AS DATETIME2)
        ELSE SYSUTCDATETIME() END),
    EligibleForPostingAtUtc = ISNULL(EligibleForPostingAtUtc, SYSUTCDATETIME()),
    UpdatedDate = GETDATE()
WHERE PostedToBoxAtUtc IS NULL
  AND EligibleForPostingAtUtc IS NULL;

PRINT CONCAT('FixedEligible=', @@ROWCOUNT);

DECLARE @PostedCount INT;
EXEC dbo.CustomersPaymentsRequest_PostEligible @UserCreateID=1, @PostedCount=@PostedCount OUTPUT;
PRINT CONCAT('PostedCount=', ISNULL(CAST(@PostedCount AS nvarchar(20)),'NULL'));

PRINT '=== Remaining unposted ===';
SELECT COUNT(*) AS Cnt, SUM(Amount) AS Total
FROM dbo.CustomersPaymentsRequest
WHERE PostedToBoxAtUtc IS NULL
  AND CreatedDate >= DATEADD(day,-2,GETDATE());

PRINT '=== List5 CP today after ===';
SELECT COUNT(*) AS Cnt FROM dbo.CustomersPayments
WHERE DelegateID=5 AND CONVERT(date, PaymentDate)='2026-09-21';
"""
open("/tmp/fix_eligible.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd,
     "-i", "/tmp/fix_eligible.sql", "-W", "-s", "|", "-f", "65001"],
    capture_output=True, text=True,
)
print(r.stdout)
print(r.stderr[:600] if r.stderr else "")
print("RC", r.returncode)
