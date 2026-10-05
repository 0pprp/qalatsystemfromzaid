#!/usr/bin/env python3
"""Ensure every active leaf-login delegate has a SelectDelegate self-row so GetDelegateSelect is non-empty.
Applies to all DelegateHaider* Databases. SSH_PASS required.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
APPLY = "--apply" in sys.argv

SQL = r"""
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BeforeEmpty INT = (
  SELECT COUNT(*)
  FROM dbo.Delegates d
  WHERE ISNULL(d.DelegateState,0)=1
    AND NULLIF(LTRIM(RTRIM(CAST(d.AsyncID AS nvarchar(100)))), N'') IS NOT NULL
    AND NOT EXISTS (
      SELECT 1 FROM dbo.SelectDelegate sd WHERE sd.DelegateFatherID = d.DelegateID
    )
);

PRINT CONCAT('DB=', DB_NAME(), ' MISSING_FATHER_ROWS=', @BeforeEmpty);

IF @BeforeEmpty > 0
BEGIN
  BEGIN TRAN;
  INSERT INTO dbo.SelectDelegate
    (DelegateFatherID, DelegateChildID, UserID, AsyncState, AsyncID, CreatedDate)
  SELECT
    d.DelegateID,
    d.DelegateID,
    1,
    1,
    d.AsyncID,
    GETDATE()
  FROM dbo.Delegates d
  WHERE ISNULL(d.DelegateState,0)=1
    AND NULLIF(LTRIM(RTRIM(CAST(d.AsyncID AS nvarchar(100)))), N'') IS NOT NULL
    AND NOT EXISTS (
      SELECT 1 FROM dbo.SelectDelegate sd WHERE sd.DelegateFatherID = d.DelegateID
    );
  PRINT CONCAT('INSERTED=', @@ROWCOUNT);
  COMMIT TRAN;
END
ELSE
  PRINT 'INSERTED=0';

-- Spot-check حي العباس if present
IF EXISTS (SELECT 1 FROM dbo.Delegates WHERE LTRIM(RTRIM(DelegateName)) LIKE N'%حي العباس%')
BEGIN
  DECLARE @DID INT = (
    SELECT TOP 1 DelegateID FROM dbo.Delegates
    WHERE LTRIM(RTRIM(DelegateName)) LIKE N'%حي العباس%'
  );
  PRINT CONCAT('CHECK_DID=', @DID);
  EXEC dbo.GetDelegateSelect @DelegateID=@DID;
END
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    apply = 1 if APPLY else 0
    remote = f"""
import json, glob, os, subprocess
apply = {apply}
sql = {SQL!r}

def parse_cs(cfg_path):
    cfg=json.load(open(cfg_path,encoding='utf-8'))
    cs=cfg.get('ConnectionStrings',{{}}).get('DataBaseConnection') or ''
    parts={{}}
    for p in cs.split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return parts.get('server') or parts.get('data source'), parts.get('initial catalog') or parts.get('database'), parts.get('user id') or parts.get('uid'), parts.get('password') or parts.get('pwd')

paths=sorted(glob.glob('/opt/qalaa-runtime/DelegateHaider*/appsettings.json'))
# unique by DB name
seen=set(); jobs=[]
for p in paths:
    if 'backup' in p.lower() or 'old' in p.lower():
        continue
    try:
        server,db,user,pwd=parse_cs(p)
    except Exception as e:
        print('SKIP', p, e); continue
    if not db or db in seen:
        continue
    seen.add(db)
    jobs.append((p,server,db,user,pwd))

print('JOBS', len(jobs), 'APPLY', apply)
open('/tmp/fix_selectdelegate_self.sql','w',encoding='utf-8').write(sql if apply else sql.replace('BEGIN TRAN;','BEGIN TRAN; --dry\\nIF 1=0 BEGIN').replace('INSERT INTO','-- INSERT INTO'))

# For dry-run, just report missing counts without insert
dry_sql = '''
SET NOCOUNT ON;
DECLARE @BeforeEmpty INT = (
  SELECT COUNT(*)
  FROM dbo.Delegates d
  WHERE ISNULL(d.DelegateState,0)=1
    AND NULLIF(LTRIM(RTRIM(CAST(d.AsyncID AS nvarchar(100)))), N'') IS NOT NULL
    AND NOT EXISTS (
      SELECT 1 FROM dbo.SelectDelegate sd WHERE sd.DelegateFatherID = d.DelegateID
    )
);
PRINT CONCAT('DB=', DB_NAME(), ' MISSING_FATHER_ROWS=', @BeforeEmpty);
SELECT TOP 5 d.DelegateID, LTRIM(RTRIM(d.DelegateName)) N
FROM dbo.Delegates d
WHERE ISNULL(d.DelegateState,0)=1
  AND NULLIF(LTRIM(RTRIM(CAST(d.AsyncID AS nvarchar(100)))), N'') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM dbo.SelectDelegate sd WHERE sd.DelegateFatherID = d.DelegateID
  )
ORDER BY d.DelegateName;
'''

for p,server,db,user,pwd in jobs:
    q=sql if apply else dry_sql
    open('/tmp/fix_sd.sql','w',encoding='utf-8').write(q)
    cmd=['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/fix_sd.sql','-W','-s','|']
    r=subprocess.run(cmd,capture_output=True,text=True)
    print('----', db, 'from', os.path.basename(os.path.dirname(p)), 'RC', r.returncode)
    out=(r.stdout or '')
    print(out[:2500])
    if r.stderr:
        print('ERR', r.stderr[:400])
print('DONE')
"""
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/fix_sd_all.py", "w") as f:
                f.write(remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/fix_sd_all.py", timeout=300)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write((out + ("\nSTDERR " + err[:1500] if err.strip() else "") + "\n").encode("utf-8", "replace"))
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
