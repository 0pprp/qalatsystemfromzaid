#!/usr/bin/env python3
"""Ensure main accountant jana/jana1212 exists and is active in every province DB."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]

REMOTE = r'''#!/usr/bin/env python3
import json, subprocess
from pathlib import Path

SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"
ADMINS = __ADMINS__
USER = "jana"
PWD = "jana1212"

def parse_cs(cs):
    parts = {}
    for p in cs.split(";"):
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

sql = f"""
SET NOCOUNT ON;
DECLARE @UserName NVARCHAR(100) = N'{USER}';
DECLARE @Password NVARCHAR(100) = N'{PWD}';
DECLARE @UserType NVARCHAR(100) = N'محاسب رئيسي';
DECLARE @Email NVARCHAR(100) = N'jana@local';
DECLARE @Phone NVARCHAR(100) = N'07800000000';
DECLARE @Uid INT;
DECLARE @Box INT = (SELECT TOP 1 BoxID FROM dbo.Boxes ORDER BY BoxID);
DECLARE @City INT = (SELECT TOP 1 CityID FROM dbo.Cities ORDER BY CityID);

IF EXISTS (SELECT 1 FROM dbo.Users WHERE LOWER(UserName)=LOWER(@UserName))
BEGIN
    UPDATE dbo.Users
    SET Password = @Password,
        UserType = @UserType,
        UserState = 1,
        Email = ISNULL(NULLIF(Email, N''), @Email),
        PhoneNumber = ISNULL(NULLIF(PhoneNumber, N''), @Phone),
        BoxID = COALESCE(BoxID, @Box),
        UpdatedDate = GETDATE()
    WHERE LOWER(UserName)=LOWER(@UserName);
    SET @Uid = (SELECT TOP 1 UserID FROM dbo.Users WHERE LOWER(UserName)=LOWER(@UserName));
    SELECT 'UPDATED' AS Action, @Uid AS UserID, UserName, Password, UserType, UserState, BoxID
    FROM dbo.Users WHERE UserID=@Uid;
END
ELSE
BEGIN
    INSERT INTO dbo.Users (UserName, Email, Password, PhoneNumber, Address, UserState, AsyncID, AsyncState, UserType, UserImage, BoxID, CreatedDate)
    VALUES (@UserName, @Email, @Password, @Phone, N'', 1, CONVERT(NVARCHAR(50), NEWID()), 0, @UserType, NULL, @Box, GETDATE());
    SET @Uid = SCOPE_IDENTITY();
    SELECT 'INSERTED' AS Action, @Uid AS UserID, UserName, Password, UserType, UserState, BoxID
    FROM dbo.Users WHERE UserID=@Uid;
END

-- Ensure city assignment so admin city picker works
IF OBJECT_ID(N'dbo.UsersSelectedCities', N'U') IS NOT NULL AND @City IS NOT NULL AND @Uid IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.UsersSelectedCities WHERE UserID=@Uid AND CityID=@City)
        INSERT INTO dbo.UsersSelectedCities (UserID, CityID, AsyncState, AsyncID, CreatedDate)
        VALUES (@Uid, @City, 1, CONVERT(NVARCHAR(50), NEWID()), GETDATE());
END

-- Login smoke
SELECT CASE WHEN EXISTS (
  SELECT 1 FROM dbo.Users
  WHERE UserName=@UserName AND Password=@Password AND UserState=1 AND UserType=N'محاسب رئيسي'
) THEN 'LOGIN_OK' ELSE 'LOGIN_FAIL' END AS LoginCheck;
"""

ok = fail = 0
for name in ADMINS:
    cfg_path = Path(f"/opt/qalaa-runtime/{name}/appsettings.json")
    if not cfg_path.is_file():
        print("SKIP", name)
        continue
    try:
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        server, db, user, pwd = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
    except Exception as e:
        print("CFG_ERR", name, e)
        fail += 1
        continue
    try:
        out = subprocess.check_output(
            [SQLCMD, "-S", server, "-d", db, "-U", user, "-P", pwd, "-C", "-W", "-Q", sql],
            stderr=subprocess.STDOUT, text=True, timeout=90,
        )
        print("====", name, db, "====")
        print(out.strip()[-700:])
        if "LOGIN_OK" in out:
            ok += 1
        else:
            fail += 1
    except subprocess.CalledProcessError as e:
        fail += 1
        print("FAIL", name, (e.output or "")[-600:])
    except Exception as e:
        fail += 1
        print("ERR", name, e)

print("SUMMARY ok", ok, "fail", fail)
print("DONE")
'''.replace("__ADMINS__", repr(ADMINS))


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
            with sftp.file("/tmp/ensure_jana_accountant.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/ensure_jana_accountant.py", timeout=600)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
