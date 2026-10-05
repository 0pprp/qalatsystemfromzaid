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
    open("/tmp/karb3.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/karb3.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:14000])


run(
    """
SET NOCOUNT ON;
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.View_SelectDelegate'));
""",
    "view_def",
)

run(
    """
SET NOCOUNT ON;
SELECT TOP 20 COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='SelectDelegate' ORDER BY ORDINAL_POSITION;
SELECT COUNT(*) Cnt FROM dbo.SelectDelegate;
SELECT TOP 10 * FROM dbo.SelectDelegate;
""",
    "sd_table",
)

run(
    """
SET NOCOUNT ON;
-- Active delegates not appearing in view
SELECT COUNT(*) ActiveDelegates FROM dbo.Delegates WHERE ISNULL(DelegateState,0)=1;
SELECT COUNT(*) InView FROM dbo.View_SelectDelegate;
SELECT TOP 30 d.DelegateID, LTRIM(RTRIM(d.DelegateName)) N
FROM dbo.Delegates d
WHERE ISNULL(d.DelegateState,0)=1
  AND NOT EXISTS (
    SELECT 1 FROM dbo.View_SelectDelegate v WHERE v.DelegateID=d.DelegateID
  )
ORDER BY d.DelegateName;
""",
    "missing_from_view",
)

print("DONE")
