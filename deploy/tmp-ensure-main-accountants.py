#!/usr/bin/env python3
"""
Ensure main accountants exist in every province Admin DB:
- jana / jana1212  (fix + all cities assignment)
- hadi / hadi2006  (create)
Also report login success per province.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''#!/usr/bin/env python3
import json, subprocess, os
from pathlib import Path

SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"
if not os.path.isfile(SQLCMD):
    SQLCMD = "sqlcmd"

ACCOUNTS = [
    ("jana", "jana1212", "jana@local"),
    ("hadi", "hadi2006", "hadi@local"),
]

def parse_cs(cs):
    parts = {}
    for p in (cs or "").split(";"):
        p = p.strip()
        if not p or "=" not in p:
            continue
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("database") or parts.get("initial catalog"),
        parts.get("user id") or parts.get("uid") or parts.get("user"),
        parts.get("password") or parts.get("pwd"),
    )

def ensure_sql(user, pwd, email):
    return f"""
SET NOCOUNT ON;
DECLARE @UserName NVARCHAR(100) = N'{user}';
DECLARE @Password NVARCHAR(100) = N'{pwd}';
DECLARE @UserType NVARCHAR(100) = N'محاسب رئيسي';
DECLARE @Email NVARCHAR(100) = N'{email}';
DECLARE @Phone NVARCHAR(100) = N'07800000000';
DECLARE @Uid INT;
DECLARE @Box INT = (SELECT TOP 1 BoxID FROM dbo.Boxes ORDER BY BoxID);

IF EXISTS (SELECT 1 FROM dbo.Users WHERE LOWER(UserName)=LOWER(@UserName))
BEGIN
    UPDATE dbo.Users
    SET Password = @Password,
        UserType = @UserType,
        UserState = 1,
        Email = ISNULL(NULLIF(LTRIM(RTRIM(Email)), N''), @Email),
        PhoneNumber = ISNULL(NULLIF(LTRIM(RTRIM(PhoneNumber)), N''), @Phone),
        BoxID = COALESCE(BoxID, @Box),
        UpdatedDate = GETDATE()
    WHERE LOWER(UserName)=LOWER(@UserName);
    SET @Uid = (SELECT TOP 1 UserID FROM dbo.Users WHERE LOWER(UserName)=LOWER(@UserName) ORDER BY UserID);
    SELECT 'UPDATED' AS Action, @Uid AS UserID, UserName, UserType, UserState, BoxID
    FROM dbo.Users WHERE UserID=@Uid;
END
ELSE
BEGIN
    INSERT INTO dbo.Users (UserName, Email, Password, PhoneNumber, Address, UserState, AsyncID, AsyncState, UserType, UserImage, BoxID, CreatedDate)
    VALUES (@UserName, @Email, @Password, @Phone, N'', 1, CONVERT(NVARCHAR(50), NEWID()), 0, @UserType, NULL, @Box, GETDATE());
    SET @Uid = SCOPE_IDENTITY();
    SELECT 'INSERTED' AS Action, @Uid AS UserID, UserName, UserType, UserState, BoxID
    FROM dbo.Users WHERE UserID=@Uid;
END

-- Assign ALL cities in this DB so city picker / access is not limited
IF OBJECT_ID(N'dbo.UsersSelectedCities', N'U') IS NOT NULL AND @Uid IS NOT NULL
BEGIN
    INSERT INTO dbo.UsersSelectedCities (UserID, CityID, AsyncState, AsyncID, CreatedDate)
    SELECT @Uid, c.CityID, 1, CONVERT(NVARCHAR(50), NEWID()), GETDATE()
    FROM dbo.Cities c
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.UsersSelectedCities usc
        WHERE usc.UserID=@Uid AND usc.CityID=c.CityID
    );
    SELECT COUNT(*) AS SelectedCities FROM dbo.UsersSelectedCities WHERE UserID=@Uid;
END

SELECT CASE WHEN EXISTS (
  SELECT 1 FROM dbo.Users
  WHERE UserName=@UserName AND Password=@Password AND UserState=1 AND UserType=N'محاسب رئيسي'
) THEN 'LOGIN_OK' ELSE 'LOGIN_FAIL' END AS LoginCheck;
"""

runtime = Path("/opt/qalaa-runtime")
admins = sorted([
    p.name for p in runtime.iterdir()
    if p.is_dir() and p.name.startswith("Admin") and (p / "appsettings.json").is_file()
    and ".bak" not in p.name.lower()
])
print("ADMINS", len(admins), ",".join(admins))

summary = {u: {"ok": 0, "fail": 0} for u, _, _ in ACCOUNTS}

for name in admins:
    cfg_path = runtime / name / "appsettings.json"
    try:
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        server, db, dbuser, dbpwd = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
    except Exception as e:
        print("CFG_ERR", name, e)
        for u, _, _ in ACCOUNTS:
            summary[u]["fail"] += 1
        continue

    print("====", name, db, "====")
    for user, pwd, email in ACCOUNTS:
        sql = ensure_sql(user, pwd, email)
        try:
            out = subprocess.check_output(
                [SQLCMD, "-S", server, "-d", db, "-U", dbuser, "-P", dbpwd, "-C", "-I", "-f", "65001", "-W", "-Q", sql],
                stderr=subprocess.STDOUT, text=True, timeout=90,
            )
            tail = out.strip()[-500:]
            print(user, "=>", "LOGIN_OK" if "LOGIN_OK" in out else "LOGIN_FAIL", "|", "INSERTED" if "INSERTED" in out else ("UPDATED" if "UPDATED" in out else "?"), "|", "cities=" + (tail.split("SelectedCities")[-1].strip().splitlines()[0] if "SelectedCities" in out else "?"))
            if "LOGIN_OK" in out:
                summary[user]["ok"] += 1
            else:
                summary[user]["fail"] += 1
                print(tail)
        except subprocess.CalledProcessError as e:
            summary[user]["fail"] += 1
            print("FAIL", user, name, (e.output or "")[-400:])
        except Exception as e:
            summary[user]["fail"] += 1
            print("ERR", user, name, e)

print("SUMMARY", summary)
print("DONE")
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/ensure_main_accountants.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/ensure_main_accountants.py", timeout=900)
        text = o.read().decode("utf-8", "replace")
        err = e.read().decode("utf-8", "replace")
        Path(__file__).with_name("_ensure_main_accountants_out.txt").write_text(
            text + (("\nSSH_ERR\n" + err) if err.strip() else ""), encoding="utf-8"
        )
        print(text)
        if err.strip():
            print("SSH_ERR", err[-1500:])
        return 0 if "DONE" in text else 1
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
