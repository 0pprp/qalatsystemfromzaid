#!/usr/bin/env python3
"""Hard-delete Excel SM users from all provinces."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''#!/usr/bin/env python3
import json, subprocess
from pathlib import Path

REMOVE = ["fat", "zai", "ban", "jin", "amar1"]
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"

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

in_list = ",".join("'" + n.lower().replace("'", "''") + "'" for n in REMOVE)

sql = f"""
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID('tempdb..#KillUsers') IS NOT NULL DROP TABLE #KillUsers;
SELECT UserID, UserName, UserType
INTO #KillUsers
FROM dbo.Users
WHERE LOWER(UserName) IN ({in_list});

SELECT 'BEFORE' AS Phase, * FROM #KillUsers;

IF NOT EXISTS (SELECT 1 FROM #KillUsers)
BEGIN
  PRINT 'NONE';
  COMMIT TRAN;
  RETURN;
END

DECLARE @ids NVARCHAR(MAX);
SELECT @ids = STUFF((
  SELECT ',' + CAST(UserID AS VARCHAR(20)) FROM #KillUsers FOR XML PATH(''), TYPE
).value('.', 'NVARCHAR(MAX)'), 1, 1, '');

-- Wipe known dependents first
IF OBJECT_ID(N'dbo.UsersSelectedCities', N'U') IS NOT NULL
  EXEC(N'DELETE FROM dbo.UsersSelectedCities WHERE UserID IN (' + @ids + N')');

IF OBJECT_ID(N'dbo.Activities', N'U') IS NOT NULL
  EXEC(N'DELETE FROM dbo.Activities WHERE UserID IN (' + @ids + N')');

IF OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.CustomerNotes', N'UserID') IS NOT NULL
  EXEC(N'UPDATE dbo.CustomerNotes SET UserID = NULL WHERE UserID IN (' + @ids + N')');

-- Any remaining FK children: null if nullable else delete
DECLARE @t SYSNAME, @c SYSNAME, @sch SYSNAME, @nullable BIT, @q NVARCHAR(MAX);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id), OBJECT_NAME(fk.parent_object_id), pc.name, pc.is_nullable
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.Users') AND rc.name = N'UserID';

OPEN cur;
FETCH NEXT FROM cur INTO @sch, @t, @c, @nullable;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF @t <> N'Users'
  BEGIN
    BEGIN TRY
      IF @nullable = 1
        SET @q = N'UPDATE ' + QUOTENAME(@sch) + N'.' + QUOTENAME(@t) + N' SET ' + QUOTENAME(@c) + N'=NULL WHERE ' + QUOTENAME(@c) + N' IN (' + @ids + N')';
      ELSE
        SET @q = N'DELETE FROM ' + QUOTENAME(@sch) + N'.' + QUOTENAME(@t) + N' WHERE ' + QUOTENAME(@c) + N' IN (' + @ids + N')';
      EXEC(@q);
      PRINT N'CLEARED ' + @sch + N'.' + @t + N'.' + @c;
    END TRY
    BEGIN CATCH
      PRINT N'FK_FAIL ' + @sch + N'.' + @t + N'.' + @c + N' ' + ERROR_MESSAGE();
      THROW;
    END CATCH
  END
  FETCH NEXT FROM cur INTO @sch, @t, @c, @nullable;
END
CLOSE cur; DEALLOCATE cur;

DELETE FROM dbo.Users WHERE UserID IN (SELECT UserID FROM #KillUsers);
SELECT @@ROWCOUNT AS DeletedUsers;

SELECT 'AFTER' AS Phase, UserID, UserName FROM dbo.Users WHERE LOWER(UserName) IN ({in_list});

COMMIT TRAN;
"""

ok = fail = 0
for name in ADMINS:
    cfg_path = Path(f"/opt/qalaa-runtime/{name}/appsettings.json")
    if not cfg_path.is_file():
        print("SKIP_CFG", name); continue
    try:
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        server, db, user, pwd = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
    except Exception as e:
        print("CFG_ERR", name, e); fail += 1; continue
    try:
        out = subprocess.check_output(
            [SQLCMD, "-S", server, "-d", db, "-U", user, "-P", pwd, "-C", "-W", "-Q", sql],
            stderr=subprocess.STDOUT, text=True, timeout=180,
        )
        print("====", name, db, "====")
        print(out.strip()[-1500:])
        if "DeletedUsers" in out or "NONE" in out:
            ok += 1
        else:
            ok += 1
    except subprocess.CalledProcessError as e:
        fail += 1
        print("==== FAIL", name, "====")
        print((e.output or str(e))[-1500:])
    except Exception as e:
        fail += 1
        print("DB_ERR", name, e)

print("SUMMARY ok", ok, "fail", fail)
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
            with sftp.file("/tmp/hard_delete_sm_excel.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/hard_delete_sm_excel.py", timeout=900)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
