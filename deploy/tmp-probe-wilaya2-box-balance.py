#!/usr/bin/env python3
"""How Wilaya-2 box balance is computed; confirm 70k extras in AddToBox."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
DECLARE @BoxID INT = 6;

PRINT '=== Boxes columns ===';
SELECT c.name FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.Boxes') ORDER BY c.column_id;

PRINT '=== Box row ===';
SELECT * FROM dbo.Boxes WHERE BoxID=@BoxID;

PRINT '=== Sum AddToBox for box ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(Amount,0)*1448) AS bigint) AS IqdIn,
       CAST(SUM(ISNULL(Amount,0)) AS float) AS UsdIn
FROM dbo.AddToBox WHERE BoxID=@BoxID;

IF OBJECT_ID('dbo.OutFromBox','U') IS NOT NULL
  SELECT 'OutFromBox' AS T, COUNT(*) AS Cnt, CAST(SUM(ISNULL(Amount,0)*1448) AS bigint) AS Iqd
  FROM dbo.OutFromBox WHERE BoxID=@BoxID;
IF OBJECT_ID('dbo.SubtractFromBox','U') IS NOT NULL
  SELECT 'SubtractFromBox' AS T, COUNT(*) AS Cnt FROM dbo.SubtractFromBox WHERE BoxID=@BoxID;
IF OBJECT_ID('dbo.OutBox','U') IS NOT NULL
  SELECT 'OutBox' AS T, COUNT(*) AS Cnt FROM dbo.OutBox WHERE BoxID=@BoxID;

PRINT '=== tables like %Box% ===';
SELECT name FROM sys.tables WHERE name LIKE '%Box%' ORDER BY name;

PRINT '=== The 70k AddToBox IDs ===';
SELECT a.AddToBoxID, a.CustomerPaymentID, a.BoxID,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd,
       a.Notes, CONVERT(varchar(19),ISNULL(a.CreatedDate,a.DateCreate),120) AS Created
FROM dbo.AddToBox a
WHERE a.CustomerPaymentID IN (418784, 418572);

PRINT '=== If we zero/delete those two, delta ===';
SELECT CAST(SUM(ISNULL(Amount,0)*1448) AS bigint) AS IqdWithout70
FROM dbo.AddToBox
WHERE BoxID=@BoxID AND CustomerPaymentID NOT IN (418784, 418572);
"""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    runner = r"""
import json, os, subprocess, tempfile
from pathlib import Path
def parse_cs(cs):
    parts={}
    for p in (cs or '').split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return (parts.get('server') or parts.get('data source'),
            parts.get('initial catalog') or parts.get('database'),
            parts.get('user id') or parts.get('uid'),
            parts.get('password') or parts.get('pwd'))
cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
sql=Path('/tmp/wilaya2_boxbal.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','120','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-2000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_boxbal.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_boxbal_runner.py", "w") as f:
        f.write(runner)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_boxbal_runner.py", timeout=180)
    text = out.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_boxbal_out.txt").write_text(text + err.read().decode("utf-8","replace"), encoding="utf-8")
    print(text)
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
