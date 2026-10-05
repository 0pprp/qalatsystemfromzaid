#!/usr/bin/env python3
import json
import subprocess

cfg = json.load(
    open("/opt/qalaa-runtime/DelegateHaiderKarbalaLast/appsettings.json", encoding="utf-8")
)
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
sqlcmd = [
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
    "-W",
    "-s",
    "|",
    "-h",
    "-1",
]


def run(sql: str, label: str) -> None:
    open("/tmp/karb2.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/karb2.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:12000])


run(
    """
SET NOCOUNT ON;
-- Is حي العباس a child of someone?
SELECT TOP 30 SelectDelegateID, DelegateFatherID, DelegateID, DelegateName
FROM dbo.View_SelectDelegate
WHERE DelegateID=4026 OR DelegateFatherID=4026
   OR DelegateName LIKE N'%حي العباس%';
""",
    "view_links",
)

run(
    """
SET NOCOUNT ON;
-- Fathers that include عباس children; sample fathers with 0 children after dedupe bug?
SELECT TOP 20 DelegateFatherID, COUNT(*) Cnt
FROM dbo.View_SelectDelegate
GROUP BY DelegateFatherID
ORDER BY Cnt DESC;

-- Count fathers where MIN/GROUP BY dedupe might drop all if SelectDelegateID null?
SELECT COUNT(*) ViewRows FROM dbo.View_SelectDelegate;
SELECT COUNT(*) FathersWithChildren FROM (
  SELECT DelegateFatherID FROM dbo.View_SelectDelegate GROUP BY DelegateFatherID
) x;

-- Compare old vs new SP for father that has children (1)
PRINT '--- father 1 ---';
EXEC dbo.GetDelegateSelect @DelegateID=1;
""",
    "coverage",
)

run(
    """
SET NOCOUNT ON;
-- Find parent async of حي العباس via SelectDelegate table raw
IF OBJECT_ID('dbo.SelectDelegate','U') IS NOT NULL
BEGIN
  SELECT TOP 5 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SelectDelegate';
  SELECT TOP 30 * FROM dbo.SelectDelegate WHERE DelegateID=4026 OR DelegateFatherID=4026;
END
""",
    "raw_table",
)

print("DONE")
