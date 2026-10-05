#!/usr/bin/env python3
"""Retry atomic posting migration on DatabaseCompanyBabil only."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
MIG = ROOT / "DatabaseScripts" / "Migrations" / "20261004_CollectionPayment_AtomicPosting_NoLatestId.sql"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    sql = MIG.read_text(encoding="utf-8")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/retry_babil_atomic.sql", "w") as f:
            f.write(sql)
        remote = r"""
import json, subprocess
cfg=json.load(open('/opt/qalaa-runtime/AdminHilla/appsettings.json',encoding='utf-8'))
cs=cfg['ConnectionStrings']['DataBaseConnection']
parts={}
for p in cs.split(';'):
    if '=' in p:
        k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
# Prefer Babil catalog from AdminHilla if matching, else force Babil using same creds
server=parts.get('server') or parts.get('data source')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
db='DatabaseCompanyBabil'
sqlcmd='/opt/mssql-tools18/bin/sqlcmd'
cmd=[sqlcmd,'-C','-I','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/retry_babil_atomic.sql','-b','-W']
r=subprocess.run(cmd,capture_output=True,text=True)
print(r.stdout[-2000:])
print(r.stderr[-1000:] if r.stderr else '')
print('RC', r.returncode)
# verify proc has OUTPUT INSERTED
v=subprocess.run([sqlcmd,'-C','-I','-S',server,'-d',db,'-U',user,'-P',pwd,'-Q',
"SET NOCOUNT ON; SELECT CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.CustomersPaymentsFromRequest_Create')) LIKE '%OUTPUT INSERTED.CustomerPaymentID%' THEN 1 ELSE 0 END AS HasOutput, CASE WHEN COL_LENGTH('dbo.CustomersPayments','ClientPaymentId') IS NULL THEN 0 ELSE 1 END AS HasClientPaymentId;",
'-W','-h','-1'],capture_output=True,text=True)
print('VERIFY', v.stdout.strip())
print('VRC', v.returncode)
"""
        with sftp.file("/tmp/retry_babil_atomic.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/retry_babil_atomic.py", timeout=300)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1000:])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
