import json
import subprocess
from pathlib import Path


def parse_cs(cs):
    parts = {}
    for part in (cs or "").split(";"):
        if "=" in part:
            k, v = part.split("=", 1)
            parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("initial catalog") or parts.get("database"),
        parts.get("user id") or parts.get("uid"),
        parts.get("password") or parts.get("pwd"),
    )


cfg = json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8", errors="replace"))
cs = (cfg.get("ConnectionStrings") or {}).get("DataBaseConnection")
server, db, user, pwd = parse_cs(cs)
sql = """
SET NOCOUNT ON;
PRINT 'raw active';
SELECT COUNT(1) FROM dbo.SalesRequests WHERE ISNULL(IsDeleted,0)=0;
PRINT 'with buggy NOT exception filter';
SELECT COUNT(1) FROM dbo.SalesRequests R
WHERE ISNULL(R.IsDeleted,0)=0
AND NOT (
  R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
);
PRINT 'with fixed filter';
SELECT COUNT(1) FROM dbo.SalesRequests R
WHERE ISNULL(R.IsDeleted,0)=0
AND NOT (
  R.ExceptionHoldStatus IS NOT NULL
  AND R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
);
PRINT 'id 202 buggy';
SELECT Id FROM dbo.SalesRequests R WHERE Id=202
AND ISNULL(R.IsDeleted,0)=0
AND NOT (
  R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
);
PRINT 'id 202 fixed';
SELECT Id FROM dbo.SalesRequests R WHERE Id=202
AND ISNULL(R.IsDeleted,0)=0
AND NOT (
  R.ExceptionHoldStatus IS NOT NULL
  AND R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
);
PRINT 'sent buggy vs fixed';
SELECT
 (SELECT COUNT(1) FROM dbo.SalesRequests R WHERE ISNULL(R.IsDeleted,0)=0 AND R.Status=N'New'
   AND R.CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate')
   AND NOT (R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected') AND ISNULL(R.TargetEmployeeId,0)<=0)
 ) AS SentBuggy,
 (SELECT COUNT(1) FROM dbo.SalesRequests R WHERE ISNULL(R.IsDeleted,0)=0 AND R.Status=N'New'
   AND R.CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate')
   AND NOT (R.ExceptionHoldStatus IS NOT NULL AND R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected') AND ISNULL(R.TargetEmployeeId,0)<=0)
 ) AS SentFixed;
PRINT 'unassigned target0 null hold';
SELECT Id, Status, CustomerSourceType, CreatedByName, CustomerName, CustomerPhone
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted,0)=0 AND ISNULL(TargetEmployeeId,0)<=0 AND ExceptionHoldStatus IS NULL AND Status=N'New'
ORDER BY Id DESC;
"""
Path("/tmp/prove_null_hold.sql").write_text(sql, encoding="utf-8")
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd, "-i", "/tmp/prove_null_hold.sql", "-s", "|", "-w", "65535"],
    capture_output=True,
    text=True,
)
print(r.stdout)
print("RC", r.returncode)

# BranchId config on najaf
print("=== najaf SalesManagement config ===")
unit = "qalaa-company-sharenewnajaf.service"
print(subprocess.getoutput(f"systemctl show {unit} -p Environment --value"))
cfg_sm = cfg.get("SalesManagement") or {}
print("appsettings SalesManagement", json.dumps(cfg_sm, ensure_ascii=False))
