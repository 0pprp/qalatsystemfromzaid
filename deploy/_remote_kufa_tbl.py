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
for name in ["DatabaseCompanyNajaf", "DatabaseCompanyNajaf_DEMO"]:
    print("===", name, "===")
    print(run(s, name, u, pw, "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations', N'U') IS NULL THEN N'NO' ELSE N'YES' END AS HasTable;"))
    print(run(s, name, u, pw, "SET NOCOUNT ON; IF OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations', N'U') IS NOT NULL SELECT TOP 20 Id, CustomerId, ListId, DelegateId, Rating, EvaluatorName, CAST(CreatedAtUtc AS nvarchar(40)) AS CreatedAtUtc FROM dbo.DelegateFirstCollectionEvaluations ORDER BY CreatedAtUtc DESC;"))
print("=== sample customers ===")
print(run(s, db, u, pw, "SET NOCOUNT ON; SELECT CustomerID, CustomerName, DelegateID, CAST(DateSaleDevice AS nvarchar(40)) AS DateSaleDevice, CAST(LastPaymentDate AS nvarchar(40)) AS LastPaymentDate, CountReceiptDevice, AmountTotalSales, AmountRemaining FROM dbo.Customers WHERE CustomerID IN (53313,53209,53368,53375,53384);"))