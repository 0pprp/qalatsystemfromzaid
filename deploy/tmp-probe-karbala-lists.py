#!/usr/bin/env python3
"""Probe Karbala SelectDelegate / حي العباس lists quickly."""
from __future__ import annotations

import json
import os
import subprocess
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    remote = r'''
import json, subprocess, glob, os

def cs_from(cfg_path):
    cfg=json.load(open(cfg_path,encoding='utf-8'))
    cs=cfg['ConnectionStrings']['DataBaseConnection']
    parts={}
    for p in cs.split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return parts.get('server') or parts.get('data source'), parts.get('initial catalog') or parts.get('database'), parts.get('user id') or parts.get('uid'), parts.get('password') or parts.get('pwd')

cfg='/opt/qalaa-runtime/DelegateHaiderKarbalaLast/appsettings.json'
server,db,user,pwd=cs_from(cfg)
print('DB',db)
sqlcmd=['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-h','-1']

def run(sql,label):
    open('/tmp/karb_lists.sql','w',encoding='utf-8').write(sql)
    r=subprocess.run(sqlcmd+['-i','/tmp/karb_lists.sql'],capture_output=True,text=True)
    print('===',label,'RC',r.returncode,'===')
    print(((r.stdout or '')+(('\nSTDERR '+r.stderr) if r.stderr else ''))[:8000])

run('''SET NOCOUNT ON;
SELECT DelegateID, LTRIM(RTRIM(DelegateName)) N, ISNULL(DelegateState,0) St
FROM dbo.Delegates
WHERE DelegateName LIKE N'%حي العباس%' OR DelegateName LIKE N'%العباس%';
''','find_list')

run('''SET NOCOUNT ON;
DECLARE @DID INT = (SELECT TOP 1 DelegateID FROM dbo.Delegates WHERE LTRIM(RTRIM(DelegateName))=N'حي العباس');
PRINT CONCAT('DID=',@DID);
IF OBJECT_ID('dbo.GetDelegateSelect','P') IS NOT NULL
BEGIN
  EXEC dbo.GetDelegateSelect @DelegateID=@DID;
END
ELSE PRINT 'NO_SP_GetDelegateSelect';
''','select_sp')

run('''SET NOCOUNT ON;
-- SelectDelegate table if exists
IF OBJECT_ID('dbo.SelectDelegate','U') IS NOT NULL
BEGIN
  DECLARE @DID INT = (SELECT TOP 1 DelegateID FROM dbo.Delegates WHERE LTRIM(RTRIM(DelegateName))=N'حي العباس');
  SELECT TOP 30 * FROM dbo.SelectDelegate WHERE DelegateID=@DID OR ParentDelegateID=@DID OR LinkDelegateID=@DID;
END
ELSE
BEGIN
  SELECT TOP 20 c.name FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id WHERE t.name LIKE '%Select%Delegate%' OR t.name LIKE '%Delegate%Link%';
END
''','select_table')

run('''SET NOCOUNT ON;
-- Recent errors
IF OBJECT_ID('dbo.Errors','U') IS NOT NULL
  SELECT TOP 10 * FROM dbo.Errors ORDER BY 1 DESC;
ELSE IF OBJECT_ID('dbo.Errors_Create','P') IS NOT NULL
  PRINT 'has Errors_Create SP';
ELSE PRINT 'no errors table';
''','errors')

# Also hit API via localhost port 5208
import urllib.request
try:
  # discover login endpoint
  req=urllib.request.Request('http://127.0.0.1:5208/api/Delegates/GetDelegateSelect?delegateId=1', method='GET')
  with urllib.request.urlopen(req, timeout=10) as resp:
    print('API_SELECT', resp.status, resp.read()[:200])
except Exception as e:
  print('API_ERR', type(e).__name__, e)
'''
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/probe_karb_lists.py", "w") as f:
                f.write(remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/probe_karb_lists.py", timeout=90)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write((out + ("\nSTDERR " + err[:1000] if err.strip() else "") + "\n").encode("utf-8", "replace"))
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
