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

cfg = json.loads(Path("/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json").read_text(encoding="utf-8"))
s, db, u, pw = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
print("Delegate 7 name:")
print(run(s, db, u, pw, "SET NOCOUNT ON; SELECT DelegateID, DelegateName, CityID FROM dbo.Delegates WHERE DelegateID=7;"))
print("Payment cols:")
print(run(s, db, u, pw, "SET NOCOUNT ON; SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CustomersPayments' ORDER BY ORDINAL_POSITION;"))
print("Recent payments delegate 7:")
print(run(s, db, u, pw, "SET NOCOUNT ON; SELECT TOP 20 * FROM dbo.CustomersPayments WHERE DelegateID=7 ORDER BY 1 DESC;"))
print("nginx FirstCollection:")
print(subprocess.getoutput("grep -R FirstCollection /var/log/nginx/ 2>/dev/null | tail -30 || true"))
print("access log sample najaf:")
print(subprocess.getoutput("ls /var/log/nginx/*najaf* 2>/dev/null; ls /var/log/nginx/*delegate* 2>/dev/null | head"))