import json
import os
import subprocess

cfg = json.load(open("/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json", encoding="utf-8"))
cs = cfg["ConnectionStrings"]["DataBaseConnection"]
parts = {}
for p in cs.split(";"):
    if "=" in p:
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get("server") or parts.get("data source")
db = parts.get("initial catalog") or parts.get("database")
user = parts.get("user id") or parts.get("uid")
pwd = parts.get("password") or parts.get("pwd")

sql = r"""
SET NOCOUNT ON;
SELECT TOP 30 DelegateID, DelegateName FROM dbo.Delegates
WHERE DelegateName LIKE N'%ولاية%' OR DelegateName LIKE N'%نجف 2%'
ORDER BY DelegateID;

SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE '%Payment%' OR TABLE_NAME LIKE '%تسديد%'
ORDER BY 1;

SELECT 'CustomersPayments' AS T, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CustomersPayments' ORDER BY ORDINAL_POSITION;
SELECT 'CustomersPaymentsRequest' AS T, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CustomersPaymentsRequest' ORDER BY ORDINAL_POSITION;
"""
open("/tmp/paygap_schema.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd,
     "-i", "/tmp/paygap_schema.sql", "-W", "-s", "|", "-f", "65001"],
    capture_output=True, text=True,
)
print(r.stdout)
print(r.stderr[:500] if r.stderr else "")
