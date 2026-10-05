import json
import subprocess

cfg = json.load(open("/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json", encoding="utf-8"))
cs = cfg["ConnectionStrings"]["DataBaseConnection"]
parts = {}
for p in cs.split(";"):
    if "=" in p:
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server, db, user, pwd = (
    parts.get("server") or parts.get("data source"),
    parts.get("initial catalog") or parts.get("database"),
    parts.get("user id") or parts.get("uid"),
    parts.get("password") or parts.get("pwd"),
)

sql = r"""
SET NOCOUNT ON;
PRINT '=== List5 requests last 48h any ===';
SELECT COUNT(*) AS Cnt, SUM(Amount) AS Total,
  SUM(CASE WHEN PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS NotPosted
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=5 AND CreatedDate >= DATEADD(hour,-48,GETDATE());

SELECT TOP 20 CustomersPaymentsRequestID, Amount, PaymentDate, CreatedDate, CreatedAtUtc,
       EligibleForPostingAtUtc, PostedToBoxAtUtc, ClientPaymentId, AsyncState
FROM dbo.CustomersPaymentsRequest
WHERE DelegateID=5 AND CreatedDate >= DATEADD(hour,-48,GETDATE())
ORDER BY CustomersPaymentsRequestID DESC;

PRINT '=== SP definition peek ===';
SELECT TOP 1 OBJECT_DEFINITION(OBJECT_ID('dbo.CustomersPaymentsRequest_PostEligible')) AS Def;
"""
open("/tmp/paygap5.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd,
     "-i", "/tmp/paygap5.sql", "-y", "0", "-w", "8000", "-f", "65001"],
    capture_output=True, text=True,
)
print(r.stdout[:12000])
print(r.stderr[:400] if r.stderr else "")
