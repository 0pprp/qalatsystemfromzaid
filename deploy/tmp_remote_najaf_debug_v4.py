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
        print("SQLERR", r.stderr[:800])
    return r.returncode


cfg = json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8", errors="replace"))
cs = (cfg.get("ConnectionStrings") or {}).get("DataBaseConnection")
server, db, user, pwd = parse_cs(cs)

print("=== mimic manager page tab=all/sent/unassigned for id 202 ===")
sqlcmd(
    server,
    db,
    user,
    pwd,
    """
SET NOCOUNT ON;
DECLARE @Tab NVARCHAR(20);
DECLARE @Q NVARCHAR(100) = NULL;
DECLARE @TargetEmployeeId INT = NULL;

-- same WHERE as ManagerListWhereSql
;WITH Base AS (
SELECT R.Id, R.Status, R.CustomerName, R.CustomerPhone, R.CustomerSourceType, R.CreatedByName,
 R.TargetEmployeeId, R.CityValue, R.CityName, R.ExceptionHoldStatus, R.IsDeleted, R.CreatedAtUtc
FROM dbo.SalesRequests R
WHERE ISNULL(R.IsDeleted, 0) = 0
AND NOT (
  R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
)
AND (@TargetEmployeeId IS NULL OR R.TargetEmployeeId = @TargetEmployeeId)
)
SELECT 'all' AS Tab, COUNT(1) AS C FROM Base
UNION ALL
SELECT 'sent', COUNT(1) FROM Base
WHERE Status = N'New'
  AND (
    CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate')
    OR 1=0
  )
UNION ALL
SELECT 'unassigned', COUNT(1) FROM Base
WHERE Status NOT IN (N'Rejected', N'Completed', N'LegalFollowup')
  AND (Status = N'New' OR ISNULL(TargetEmployeeId, 0) <= 0)
  AND (ExceptionHoldStatus IS NULL OR ExceptionHoldStatus NOT IN (N'Pending', N'Approved', N'Rejected'));

SELECT 'in_all' AS Q, Id, Status, CustomerSourceType, CityValue, CityName, CreatedByName
FROM dbo.SalesRequests R
WHERE Id=202
AND ISNULL(R.IsDeleted, 0) = 0
AND NOT (
  R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
);

SELECT 'in_sent' AS Q, Id FROM dbo.SalesRequests R
WHERE Id=202 AND ISNULL(R.IsDeleted,0)=0 AND R.Status=N'New'
 AND R.CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate');

PRINT 'GetAdmin / city mapping';
IF OBJECT_ID(N'dbo.GetAdmin', N'U') IS NOT NULL
  SELECT TOP 20 * FROM dbo.GetAdmin;
IF OBJECT_ID(N'dbo.View_GetAdmin', N'V') IS NOT NULL
  SELECT TOP 20 * FROM dbo.View_GetAdmin;
""",
    "/tmp/najaf_mimic.sql",
)

# Gateway branches: how Najaf cityValue is registered
print("=== gateway cities / najaf ===")
gw = Path("/opt/qalaa-runtime/SalesGateway")
for name in ("appsettings.json", "appsettings.Production.json"):
    p = gw / name
    if p.is_file():
        txt = p.read_text(encoding="utf-8", errors="replace")
        if "najaf" in txt.lower() or "Najaf" in txt:
            print("FILE", name)
            # print nearby lines
            for i, line in enumerate(txt.splitlines()):
                if "najaf" in line.lower() or "5301" in line or "DatabaseCompanyNajaf" in line:
                    print(f"{i+1}:{line[:200]}")

# Also SQL gateway Mobile/Admin cities table if any
print("=== gw env connection for AdminCities ===")
pid = subprocess.getoutput("systemctl show qalaa-sales-gateway -p MainPID --value").strip()
print("gw_pid", pid)
cs_gw = None
if pid and pid != "0":
    for e in Path(f"/proc/{pid}/environ").read_bytes().split(b"\0"):
        s = e.decode("utf-8", "replace")
        if "ConnectionStrings" in s and "=" in s:
            print("ENVKEY", s.split("=", 1)[0])
            if "SalesGateway" in s or "DataBase" in s:
                cs_gw = s.split("=", 1)[1]

# Try delegated-manager env
text = Path("/etc/qalaa-production/delegated-manager.env").read_text(encoding="utf-8", errors="replace")
for line in text.splitlines():
    if "ConnectionStrings__SalesGateway" in line:
        cs_gw = line.split("=", 1)[1].strip().strip('"').strip("'")
        print("from delegated-manager.env")
if cs_gw:
    s, d, u, p = parse_cs(cs_gw)
    print("GWDB", s, d, u)
    sqlcmd(
        s,
        d,
        u,
        p,
        """
SET NOCOUNT ON;
SELECT TOP 50 TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE N'%City%' OR TABLE_NAME LIKE N'%Admin%' OR TABLE_NAME LIKE N'%Branch%';
IF OBJECT_ID(N'dbo.AdminCities', N'U') IS NOT NULL
  SELECT * FROM dbo.AdminCities WHERE Name LIKE N'%نجف%' OR DatabaseName LIKE N'%Najaf%' OR Value LIKE N'%Najaf%' OR Link LIKE N'%najaf%' OR Link LIKE N'%5301%';
IF OBJECT_ID(N'dbo.GetAdmin', N'U') IS NOT NULL
  SELECT TOP 30 * FROM dbo.GetAdmin WHERE Name LIKE N'%نجف%' OR DatabaseName LIKE N'%Najaf%' OR Link LIKE N'%najaf%';
""",
        "/tmp/gw_cities.sql",
    )
