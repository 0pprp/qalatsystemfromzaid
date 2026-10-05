#!/usr/bin/env python3
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SELECT DB_NAME() AS DbName;
SELECT OBJECT_ID('dbo.GetDateWeek') AS ObjIdBefore;
BEGIN TRY
  EXEC(N'
CREATE OR ALTER PROC [dbo].[GetDateWeek]
AS
SELECT
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()), 120) AS Date1,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-1), 120) AS Date2,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-2), 120) AS Date3,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-3), 120) AS Date4,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-4), 120) AS Date5,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-5), 120) AS Date6,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-6), 120) AS Date7
');
  PRINT 'CREATE_OK';
END TRY
BEGIN CATCH
  PRINT CONCAT('CREATE_ERR=', ERROR_NUMBER(), ' ', ERROR_MESSAGE());
END CATCH
SELECT OBJECT_ID('dbo.GetDateWeek') AS ObjIdAfter;
IF OBJECT_ID('dbo.GetDateWeek','P') IS NOT NULL
BEGIN
  EXEC dbo.GetDateWeek;
  PRINT 'EXEC_OK';
END
ELSE
  PRINT 'STILL_MISSING';

-- Confirm week amounts for Sabah 8
SELECT CustomerID, Amount1 AS Today, Amount2 AS Yesterday
FROM dbo.View_Customers WHERE CustomerID=51962;
PRINT 'DONE';
"""


def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/gdw_fix.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/gdw_fix_runner.py", "w") as f:
        f.write(
            r"""
import json, os, subprocess
from pathlib import Path
cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
parts={}
for p in cfg['ConnectionStrings']['DataBaseConnection'].split(';'):
    if '=' in p:
        k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
bin='/opt/mssql-tools18/bin/sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','80','-i','/tmp/gdw_fix.sql'], capture_output=True, text=True)
Path('/tmp/gdw_fix_out.txt').write_text((r.stdout or '')+'\n'+(r.stderr or ''), encoding='utf-8')
print('EXIT', r.returncode)
"""
        )
    sftp.close()
    _, out, _ = c.exec_command("python3 /tmp/gdw_fix_runner.py", timeout=120)
    print(out.read().decode("utf-8", "replace"))
    _, out2, _ = c.exec_command("cat /tmp/gdw_fix_out.txt", timeout=30)
    remote = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_gdw_fix_out.txt").write_text(remote, encoding="utf-8")
    for line in remote.splitlines():
        print(line.encode("ascii", "replace").decode("ascii"))
    c.close()
    return 0 if "EXEC_OK" in remote and "51962" in remote else 1


if __name__ == "__main__":
    raise SystemExit(main())
