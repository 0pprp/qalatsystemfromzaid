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


def sqlcmd(server, db, user, pwd, sql, path):
    Path(path).write_text(sql, encoding="utf-8")
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
            path,
            "-s",
            "|",
            "-w",
            "65535",
        ],
        capture_output=True,
        text=True,
    )
    print(r.stdout)
    if r.stderr.strip():
        print("SQLERR", r.stderr[:1200])
    print("RC", r.returncode)
    return r.returncode


wd = Path("/opt/qalaa-runtime/AdminNajaf")
cfg = json.loads((wd / "appsettings.json").read_text(encoding="utf-8", errors="replace"))
cs = (cfg.get("ConnectionStrings") or {}).get("DataBaseConnection")
server, db, user, pwd = parse_cs(cs)
print("DB", server, db, user)

# Users columns first
print("=== Users/SalesEmployees columns ===")
sqlcmd(
    server,
    db,
    user,
    pwd,
    """
SET NOCOUNT ON;
SELECT 'Users' AS T, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'Users' ORDER BY ORDINAL_POSITION;
SELECT 'SalesEmployees' AS T, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'SalesEmployees' ORDER BY ORDINAL_POSITION;
""",
    "/tmp/najaf_cols.sql",
)

print("=== test12 lookup ===")
sqlcmd(
    server,
    db,
    user,
    pwd,
    """
SET NOCOUNT ON;
SELECT TOP 30 TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE N'%Employee%' OR TABLE_NAME LIKE N'%User%';
-- Users: print matching usernames with available columns via dynamic-ish selects
SELECT TOP 20 * FROM dbo.Users WHERE UserName LIKE N'%test12%' OR UserName LIKE N'%Test12%';
IF OBJECT_ID(N'dbo.SalesEmployees', N'U') IS NOT NULL
  SELECT TOP 20 * FROM dbo.SalesEmployees WHERE UserName LIKE N'%test12%' OR UserName LIKE N'%Test12%';
""",
    "/tmp/najaf_test12.sql",
)

print("=== SalesRequests recent / employee submitted / last 15h ===")
sqlcmd(
    server,
    db,
    user,
    pwd,
    """
SET NOCOUNT ON;
PRINT 'RECENT25';
SELECT TOP 25
  Id, Status, CustomerName, CustomerPhone, CustomerSourceType, CreatedByUserId, CreatedByName, CreatedByUserType,
  TargetEmployeeId, CityValue, CityName,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  CONVERT(varchar(19), ManagerReadAtUtc, 120) AS ManagerReadAtUtc,
  ISNULL(IsDeleted,0) AS IsDeleted, ExceptionHoldStatus
FROM dbo.SalesRequests
ORDER BY Id DESC;

PRINT 'EMP_SUBMITTED40';
SELECT TOP 40
  Id, Status, CustomerName, CustomerPhone, CreatedByUserId, CreatedByName, CreatedByUserType,
  TargetEmployeeId, CityValue,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  ISNULL(IsDeleted,0) AS IsDeleted, ExceptionHoldStatus
FROM dbo.SalesRequests
WHERE CustomerSourceType = N'EmployeeSubmitted'
ORDER BY Id DESC;

PRINT 'LAST15H';
SELECT
  Id, Status, CustomerName, CustomerPhone, CustomerSourceType, CreatedByUserId, CreatedByName, CreatedByUserType,
  TargetEmployeeId, CityValue,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  ISNULL(IsDeleted,0) AS IsDeleted, ExceptionHoldStatus
FROM dbo.SalesRequests
WHERE CreatedAtUtc >= DATEADD(hour, -15, SYSUTCDATETIME())
ORDER BY Id DESC;

PRINT 'COUNTS';
SELECT
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 THEN 1 ELSE 0 END) AS AllActive,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 AND Status=N'New' AND CustomerSourceType=N'EmployeeSubmitted' THEN 1 ELSE 0 END) AS SentEmpNew,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 AND Status=N'New' THEN 1 ELSE 0 END) AS AllNew,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 AND CustomerSourceType=N'EmployeeSubmitted' THEN 1 ELSE 0 END) AS EmpSubmittedActive,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=1 THEN 1 ELSE 0 END) AS Deleted
FROM dbo.SalesRequests;

PRINT 'BY_CREATEDBY_NAME_TODAY';
SELECT CreatedByName, CreatedByUserId, CreatedByUserType, COUNT(1) AS C
FROM dbo.SalesRequests
WHERE CreatedAtUtc >= DATEADD(hour, -15, SYSUTCDATETIME())
GROUP BY CreatedByName, CreatedByUserId, CreatedByUserType
ORDER BY C DESC;
""",
    "/tmp/najaf_sr.sql",
)

# nginx: which server_name / upstream maps to 5301
print("=== nginx najaf upstream ===")
print(subprocess.getoutput("grep -RIn --include='*.conf' -E '5301|najaf|Najaf' /etc/nginx 2>/dev/null | head -n 80"))

# access with host for submit today to najaf-related hosts
print("=== submit lines with Host if available ===")
# try to find log format
print(subprocess.getoutput("grep -RIn --include='*.conf' log_format /etc/nginx 2>/dev/null | head -n 20"))
print(subprocess.getoutput("grep 'requests/submit' /var/log/nginx/access.log | tail -n 20"))

# Hit local najaf API manager list for sent tab (no auth will 401 - just proves route)
print("=== local 5301 routes ===")
print(subprocess.getoutput("curl -sS -o /tmp/najaf_sm.json -w '%{http_code}' http://127.0.0.1:5301/api/sales-manager/sales-requests?page=1&pageSize=5&tab=sent"))
print(subprocess.getoutput("head -c 300 /tmp/najaf_sm.json; echo"))
print(subprocess.getoutput("curl -sS -o /tmp/najaf_sub.json -w '%{http_code}' -X POST http://127.0.0.1:5301/api/sales/requests/submit -H 'Content-Type: application/json' -d '{}'"))
print(subprocess.getoutput("head -c 300 /tmp/najaf_sub.json; echo"))
