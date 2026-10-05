import json, os, re, subprocess
from pathlib import Path
sqlcmd = "/opt/mssql-tools18/bin/sqlcmd" if os.path.isfile("/opt/mssql-tools18/bin/sqlcmd") else "sqlcmd"

def parse_cs(cs):
    def grab(key):
        m = re.search(r"(?:^|;)" + re.escape(key) + r"=([^;]+)", cs, re.I)
        return m.group(1) if m else ""
    return grab("Data Source") or grab("Server"), grab("Initial Catalog") or grab("Database"), grab("User ID") or grab("User Id") or grab("UID"), grab("Password") or grab("Pwd")

def run(server, db, user, pwd, sql):
    p = subprocess.run([sqlcmd, "-C", "-f", "65001", "-S", server, "-d", db, "-U", user, "-P", pwd, "-W", "-s", "|", "-Q", sql], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return (p.stdout or "") + (("\n" + p.stderr) if p.stderr else "")

print("delegated-manager.env:")
for line in Path("/etc/qalaa-production/delegated-manager.env").read_text(encoding="utf-8", errors="replace").splitlines():
    if not line.strip() or line.startswith("#"):
        continue
    k = line.split("=", 1)[0]
    print(k + "=***" if any(x in k.lower() for x in ("pass", "key", "secret")) else line)

print("SalesGateway dir", os.listdir("/opt/qalaa-runtime/SalesGateway")[:50] if os.path.isdir("/opt/qalaa-runtime/SalesGateway") else "NO")
cfg = json.loads(Path("/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json").read_text(encoding="utf-8"))
s, db, u, pw = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
sql1 = "SET NOCOUNT ON; SELECT COUNT(1) AS Total FROM dbo.SalesExceptionRequests; SELECT TOP 40 Id, CustomerId, CustomerName, CityValue, CityName, Status, CAST(ReadAtUtc AS nvarchar(40)) AS ReadAtUtc, RequestingManagerDisplayName, CAST(RequestedAtUtc AS nvarchar(40)) AS RequestedAtUtc, LEFT(Reason, 120) AS ReasonHead FROM dbo.SalesExceptionRequests ORDER BY RequestedAtUtc DESC;"
print("=== all recent ===")
print(run(s, "DatabaseSalesGateway", u, pw, sql1))
sql2 = "SET NOCOUNT ON; SELECT TOP 40 Id, CustomerId, CustomerName, CityValue, CityName, Status, CAST(ReadAtUtc AS nvarchar(40)) AS ReadAtUtc, RequestingManagerDisplayName, CAST(RequestedAtUtc AS nvarchar(40)) AS RequestedAtUtc, LEFT(Reason, 120) AS ReasonHead FROM dbo.SalesExceptionRequests WHERE RequestedAtUtc >= DATEADD(day, -3, SYSUTCDATETIME()) AND (Reason LIKE N'%مبيعات خاطئة%' OR Reason LIKE N'%تقييم المتجر%') ORDER BY RequestedAtUtc DESC;"
print("=== wrongsale 3d ===")
print(run(s, "DatabaseSalesGateway", u, pw, sql2))