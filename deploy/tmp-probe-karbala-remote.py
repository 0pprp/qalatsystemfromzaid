#!/usr/bin/env python3
import json
import subprocess
import urllib.request

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
print("DB", db)
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
    open("/tmp/karb_lists.sql", "w", encoding="utf-8").write(sql)
    r = subprocess.run(sqlcmd + ["-i", "/tmp/karb_lists.sql"], capture_output=True, text=True)
    print("===", label, "RC", r.returncode, "===")
    print(((r.stdout or "") + (("\nSTDERR " + r.stderr) if r.stderr else ""))[:10000])


run(
    """
SET NOCOUNT ON;
SELECT DelegateID, LTRIM(RTRIM(DelegateName)) N, ISNULL(DelegateState,0) St, AsyncID
FROM dbo.Delegates
WHERE DelegateName LIKE N'%العباس%';
""",
    "find",
)

run(
    """
SET NOCOUNT ON;
DECLARE @DID INT;
SELECT TOP 1 @DID = DelegateID FROM dbo.Delegates WHERE LTRIM(RTRIM(DelegateName)) LIKE N'%حي العباس%';
PRINT CONCAT('DID=', ISNULL(@DID,-1));
EXEC dbo.GetDelegateSelect @DelegateID=@DID;
""",
    "select_sp",
)

run(
    """
SET NOCOUNT ON;
-- Show current SP definition snippet
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.GetDelegateSelect'));
""",
    "sp_def",
)

# API test for a known id if found
try:
    with urllib.request.urlopen(
        "http://127.0.0.1:5208/api/Delegates/GetDelegateSelect/1", timeout=15
    ) as resp:
        body = resp.read().decode("utf-8", "replace")
        print("API1", resp.status, body[:300])
except Exception as e:
    print("API1_ERR", e)

print("DONE")
