import json
import subprocess
import urllib.error
import urllib.parse
import urllib.request

cfg = json.load(open("/opt/qalaa-demo/follower-backend/appsettings.json", encoding="utf-8"))
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
    "-I",
    "-S",
    server,
    "-d",
    db,
    "-U",
    user,
    "-P",
    pwd,
    "-s",
    "|",
    "-y",
    "0",
    "-w",
    "65535",
    "-f",
    "65001",
]

sql = """
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

PRINT '=== BEFORE duplicate AsyncID groups ===';
SELECT AsyncID, COUNT(*) AS Cnt, STRING_AGG(CAST(UserID AS nvarchar(20)), ',') AS UserIds
FROM dbo.Users
WHERE LTRIM(RTRIM(ISNULL(AsyncID,N''))) <> N''
GROUP BY AsyncID
HAVING COUNT(*) > 1;

;WITH d AS (
  SELECT UserID, UserType, AsyncID,
         ROW_NUMBER() OVER (
           PARTITION BY AsyncID
           ORDER BY CASE WHEN UserType = N'متابع' OR UserType LIKE N'متابع%' THEN 0 ELSE 1 END,
                    UserID
         ) AS rn
  FROM dbo.Users
  WHERE LTRIM(RTRIM(ISNULL(AsyncID,N''))) <> N''
)
UPDATE u
SET AsyncID = CONVERT(nvarchar(255), NEWID()),
    UpdatedDate = GETDATE()
FROM dbo.Users u
INNER JOIN d ON d.UserID = u.UserID
WHERE d.rn > 1;

UPDATE dbo.Users
SET AsyncID = CONVERT(nvarchar(255), NEWID()),
    UpdatedDate = GETDATE()
WHERE (UserType = N'متابع' OR UserType LIKE N'متابع%')
  AND ISNULL(UserState,1) = 1
  AND LTRIM(RTRIM(ISNULL(AsyncID,N''))) = N'';

PRINT '=== AFTER duplicate AsyncID groups ===';
SELECT AsyncID, COUNT(*) AS Cnt
FROM dbo.Users
WHERE LTRIM(RTRIM(ISNULL(AsyncID,N''))) <> N''
GROUP BY AsyncID
HAVING COUNT(*) > 1;

PRINT '=== followers sample ===';
SELECT UserID, UserName, UserType, UserState, AsyncID
FROM dbo.Users
WHERE UserName IN (N'ahmed4', N'حيدر', N'ايمن')
   OR (UserType = N'متابع' OR UserType LIKE N'متابع%');

COMMIT;
"""
open("/tmp/fix_async.sql", "w", encoding="utf-8").write(sql)
r = subprocess.run(sqlcmd + ["-i", "/tmp/fix_async.sql"], capture_output=True, text=True)
print(r.stdout)
print(r.stderr[:800] if r.stderr else "")
print("SQL_RC", r.returncode)
if r.returncode != 0:
    raise SystemExit(r.returncode)


def post_login(user_name: str, password: str) -> dict:
    req = urllib.request.Request(
        "http://127.0.0.1:5402/api/Followers/Login",
        data=json.dumps({"userName": user_name, "password": password}).encode(),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=20) as resp:
        return json.loads(resp.read().decode())


login = post_login("ahmed4", "ahmed4")
print("LOGIN", login)
async_id = login["asyncId"]
with urllib.request.urlopen(
    f"http://127.0.0.1:5402/api/Followers/Lists?asyncId={async_id}", timeout=20
) as resp:
    lists_body = resp.read().decode()
    print("LISTS_HTTP", resp.status, lists_body[:500])
with urllib.request.urlopen(
    f"http://127.0.0.1:8081/api/Followers/Lists?asyncId={async_id}", timeout=20
) as resp:
    print("LISTS8081_HTTP", resp.status, resp.read().decode()[:500])

login2 = post_login("حيدر", "حيدر")
print("LOGIN_HAIDER", {k: login2.get(k) for k in ("userId", "userName", "asyncId", "userType")})
aid2 = login2["asyncId"]
with urllib.request.urlopen(
    f"http://127.0.0.1:5402/api/Followers/Lists?asyncId={aid2}", timeout=20
) as resp:
    print("LISTS_HAIDER", resp.status, resp.read().decode()[:400])

lists = json.loads(lists_body)
if lists:
    lid = lists[0].get("delegateId") or lists[0].get("DelegateId")
    q = urllib.parse.urlencode(
        {"asyncId": async_id, "childId": lid, "showType": "المسددين"}
    )
    with urllib.request.urlopen(
        f"http://127.0.0.1:5402/api/Followers/Daily?{q}", timeout=60
    ) as resp:
        print("DAILY_HTTP", resp.status, "len", len(resp.read()))

for path in ("ListStats", "FieldFollowups", "PendingSales", "LegalFollowups"):
    try:
        with urllib.request.urlopen(
            f"http://127.0.0.1:8081/api/Followers/{path}?asyncId={async_id}", timeout=45
        ) as resp:
            print(path, resp.status, "len", len(resp.read()))
    except urllib.error.HTTPError as e:
        print(path, "HTTP", e.code, e.read()[:200])

print("DONE")
