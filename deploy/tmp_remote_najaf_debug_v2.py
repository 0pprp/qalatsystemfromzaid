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


unit = "qalaa-company-sharenewnajaf.service"
wd = "/opt/qalaa-runtime/AdminNajaf"
pid = subprocess.getoutput(f"systemctl show {unit} -p MainPID --value").strip()
print("PID", pid, "active", subprocess.getoutput(f"systemctl is-active {unit}"))

# Dump relevant process env keys (names only + catalog)
cs_data = cs_demo = None
require_demo = None
if pid and pid != "0":
    env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
    for e in env:
        s = e.decode("utf-8", "replace")
        if s.startswith("ConnectionStrings__DataBaseConnection="):
            cs_data = s.split("=", 1)[1]
        elif s.startswith("ConnectionStrings__SalesDemoConnection="):
            cs_demo = s.split("=", 1)[1]
        elif s.startswith("SalesManagement__RequireDemoDatabase="):
            require_demo = s.split("=", 1)[1]
        elif s.startswith("ASPNETCORE_ENVIRONMENT="):
            print("ASPNETCORE_ENVIRONMENT", s.split("=", 1)[1])

print("RequireDemoDatabase_env", require_demo)
print("has_DataBaseConnection_env", bool(cs_data))
print("has_SalesDemoConnection_env", bool(cs_demo))

cfg = json.loads((Path(wd) / "appsettings.json").read_text(encoding="utf-8", errors="replace"))
cstrings = cfg.get("ConnectionStrings") or {}
sales_mgmt = cfg.get("SalesManagement") or {}
print("cfg_RequireDemoDatabase", sales_mgmt.get("RequireDemoDatabase"))
print("cfg_AllowedDemoDatabase", sales_mgmt.get("AllowedDemoDatabase"))
print("cfg_DataBase_catalog", parse_cs(cstrings.get("DataBaseConnection") or "")[1])
print("cfg_SalesDemo_catalog", parse_cs(cstrings.get("SalesDemoConnection") or "")[1])

cs = cs_data or cstrings.get("DataBaseConnection") or cs_demo or cstrings.get("SalesDemoConnection")
server, db, user, pwd = parse_cs(cs)
print("USING", server, db, user)

# Also check demo DB in parallel if different
targets = [(db, cs)]
demo_cs = cs_demo or cstrings.get("SalesDemoConnection")
if demo_cs:
    ds, dd, du, dp = parse_cs(demo_cs)
    if dd and dd.lower() != (db or "").lower():
        targets.append((dd, demo_cs))

for catalog, raw_cs in targets:
    s, d, u, p = parse_cs(raw_cs)
    print(f"\n===== DB {d} =====")
    sql = f"""
SET NOCOUNT ON;
PRINT '=== test12 users/employees ===';
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
  SELECT TOP 20 UserID, UserName, EmployeeName, UserType, CityID, IsActive
  FROM dbo.Users
  WHERE UserName LIKE N'%test12%' OR EmployeeName LIKE N'%test12%' OR UserName LIKE N'%Test12%';
END

IF OBJECT_ID(N'dbo.SalesEmployees', N'U') IS NOT NULL
BEGIN
  SELECT TOP 20 *
  FROM dbo.SalesEmployees
  WHERE CAST(UserName AS NVARCHAR(200)) LIKE N'%test12%'
     OR CAST(DisplayName AS NVARCHAR(200)) LIKE N'%test12%'
     OR CAST(EmployeeName AS NVARCHAR(200)) LIKE N'%test12%';
END

PRINT '=== SalesRequests columns ===';
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=N'SalesRequests' ORDER BY ORDINAL_POSITION;

PRINT '=== Recent 25 SalesRequests ===';
SELECT TOP 25
  Id, Status, CustomerName, CustomerPhone, CustomerSourceType, CreatedByUserId, CreatedByName, CreatedByUserType,
  TargetEmployeeId, CityValue, CityName,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  CONVERT(varchar(19), ManagerReadAtUtc, 120) AS ManagerReadAtUtc,
  ISNULL(IsDeleted,0) AS IsDeleted, ExceptionHoldStatus
FROM dbo.SalesRequests
ORDER BY Id DESC;

PRINT '=== EmployeeSubmitted last 40 ===';
SELECT TOP 40
  Id, Status, CustomerName, CustomerPhone, CreatedByUserId, CreatedByName, CreatedByUserType,
  TargetEmployeeId, CityValue,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc,
  ISNULL(IsDeleted,0) AS IsDeleted, ExceptionHoldStatus
FROM dbo.SalesRequests
WHERE CustomerSourceType = N'EmployeeSubmitted'
ORDER BY Id DESC;

PRINT '=== Created today Iraq (~UTC+3) ===';
SELECT
  Id, Status, CustomerName, CustomerPhone, CustomerSourceType, CreatedByUserId, CreatedByName,
  CONVERT(varchar(19), CreatedAtUtc, 120) AS CreatedAtUtc, ISNULL(IsDeleted,0) AS IsDeleted
FROM dbo.SalesRequests
WHERE CreatedAtUtc >= DATEADD(hour, -15, SYSUTCDATETIME())
ORDER BY Id DESC;

PRINT '=== Tab counts mimic ===';
SELECT
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 THEN 1 ELSE 0 END) AS AllActive,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 AND Status=N'New' AND CustomerSourceType=N'EmployeeSubmitted' THEN 1 ELSE 0 END) AS SentEmpNew,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=0 AND Status=N'New' THEN 1 ELSE 0 END) AS AllNew,
  SUM(CASE WHEN ISNULL(IsDeleted,0)=1 THEN 1 ELSE 0 END) AS Deleted
FROM dbo.SalesRequests;
"""
    sqlcmd(s, d, u, p, sql, f"/tmp/najaf_sr_{d}.sql")

# nginx / access log for submit around today
print("\n=== ACCESS LOG submit hits ===")
for log in (
    "/var/log/nginx/access.log",
    "/var/log/nginx/admincompany.access.log",
    "/var/log/nginx/access.log.1",
):
    if Path(log).is_file():
        out = subprocess.getoutput(f"grep -E 'requests/submit|sales/requests' {log} | tail -n 40")
        if out.strip():
            print(log)
            print(out)

# Local BE listen + curl health for sales module gate
print("\n=== LOCAL curl najaf sales gate ===")
# find listening port from unit
show = subprocess.getoutput(f"systemctl show {unit} -p Environment -p ExecStart")
print(show[:500])
ports = subprocess.getoutput("ss -lptn | grep -i BE_Company || ss -lptn | grep 5")
print(ports[:800])
