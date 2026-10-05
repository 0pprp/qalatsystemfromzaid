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

# parse ConnectionStrings__SalesGateway from env file
cs = ""
for line in Path("/etc/qalaa-production/delegated-manager.env").read_text(encoding="utf-8", errors="replace").splitlines():
    if line.startswith("ConnectionStrings__SalesGateway="):
        cs = line.split("=", 1)[1]
        break
s, db, u, pw = parse_cs(cs)
print("DB", db, "USER", u)
sql1 = "SET NOCOUNT ON; SELECT COUNT(1) AS Total FROM dbo.SalesExceptionRequests;"
print(run(s, db, u, pw, sql1))
sql2 = "SET NOCOUNT ON; SELECT TOP 50 Id, CustomerId, CustomerName, CityValue, CityName, Status, CAST(ReadAtUtc AS nvarchar(40)) AS ReadAtUtc, RequestingManagerDisplayName, CAST(RequestedAtUtc AS nvarchar(40)) AS RequestedAtUtc, LEFT(Reason, 140) AS ReasonHead FROM dbo.SalesExceptionRequests ORDER BY RequestedAtUtc DESC;"
print("=== recent ===")
print(run(s, db, u, pw, sql2))