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
cmd = [
    "/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd, "-W", "-s", "|", "-h", "-1",
]


def run(sql, label):
    open("/tmp/t.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(cmd + ["-i", "/tmp/t.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:8000])


run(
    """SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.SelectPaymentCustomerTemporary',N'U') IS NULL BEGIN PRINT 'NO_TEMP'; RETURN; END
SELECT COUNT(*) TempCnt FROM dbo.SelectPaymentCustomerTemporary;
SELECT TOP 30 *
FROM dbo.SelectPaymentCustomerTemporary
ORDER BY 1 DESC;
""",
    "temp_table",
)

run(
    """SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.View_CustomersPaymentsRequestFinal',N'V') IS NULL BEGIN PRINT 'NO_VIEW'; RETURN; END
SELECT COUNT(*) VCnt FROM dbo.View_CustomersPaymentsRequestFinal;
""",
    "request_final_view",
)

run(
    """SET NOCOUNT ON;
DECLARE @Since DATETIME = DATEADD(day,-2, GETDATE());
SELECT d.DelegateID, LTRIM(RTRIM(d.DelegateName)) Name,
  COUNT(cp.CustomerPaymentID) Cnt
FROM dbo.Delegates d
LEFT JOIN dbo.CustomersPayments cp ON cp.DelegateID=d.DelegateID AND cp.CreatedDate>=@Since
WHERE d.DelegateID BETWEEN 1 AND 22 AND ISNULL(d.DelegateState,0)=1
GROUP BY d.DelegateID, d.DelegateName
HAVING COUNT(cp.CustomerPaymentID)=0
ORDER BY d.DelegateID;
""",
    "zero_last2d_classic",
)

print("DONE")
