#!/usr/bin/env python3
"""Deploy payment DelegateId soft-accept fix to all Delegate* runtimes + catch-up SQL on Najaf."""
from __future__ import annotations

import os
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUBLISH = ROOT / "_publish_delegate_payfix"
SKIP = {"appsettings.json", "appsettings.Development.json", "appsettings.Production.json"}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not PUBLISH.is_dir():
        print("missing", PUBLISH)
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    local_tar = ROOT / f"_publish_delegate_payfix_{stamp}.tar.gz"
    remote_tar = f"/opt/qalaa-updates/delegate-payfix-{stamp}.tar.gz"
    if local_tar.exists():
        local_tar.unlink()
    with tarfile.open(local_tar, "w:gz") as tar:
        for root, _d, files in os.walk(PUBLISH):
            for name in files:
                if name.lower() in {x.lower() for x in SKIP}:
                    continue
                full = Path(root) / name
                tar.add(full, arcname=full.relative_to(PUBLISH).as_posix())
    print(f"TAR {local_tar.stat().st_size/1024/1024:.1f}MB", flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        c.exec_command("mkdir -p /opt/qalaa-updates")[1].channel.recv_exit_status()
        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        sftp.close()

        inner = f"""
import os, shutil, subprocess, tarfile, time, glob
stamp='{stamp}'
remote_tar='{remote_tar}'
extract=f'/tmp/delegate-payfix-{{stamp}}'
shutil.rmtree(extract, ignore_errors=True)
os.makedirs(extract, exist_ok=True)
with tarfile.open(remote_tar,'r:gz') as t: t.extractall(extract)
dirs=[]
for name in sorted(os.listdir('/opt/qalaa-runtime')):
    if name.lower().startswith('delegate') and os.path.isdir(f'/opt/qalaa-runtime/{{name}}'):
        if 'backup' in name.lower() or name.endswith('.bak'): continue
        dirs.append(name)
print('DIRS', len(dirs))
# map unit names roughly
units=subprocess.getoutput("systemctl list-units --type=service --all --no-legend 'qalaa-delegate*'").splitlines()
unit_names=[u.split()[0].removesuffix('.service') for u in units if u.strip()]
print('UNITS', len(unit_names))
for d in dirs:
    dest=f'/opt/qalaa-runtime/{{d}}'
    bak=f'/opt/qalaa-backups/{{d}}-pre-payfix-{{stamp}}'
    os.makedirs(bak, exist_ok=True)
    for f in ('BE_DelegateWebApplication.dll','BE_DelegateWebApplication.pdb'):
        src=os.path.join(dest,f)
        if os.path.isfile(src): shutil.copy2(src, os.path.join(bak,f))
    for root,_,files in os.walk(extract):
        for f in files:
            full=os.path.join(root,f)
            rel=os.path.relpath(full, extract)
            target=os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print('UPDATED', d)
# restart matching units
for u in unit_names:
    subprocess.run(['systemctl','restart',u], check=False)
    print('RESTART', u, subprocess.getoutput(f'systemctl is-active {{u}}'))
time.sleep(2)
print('DONE_DEPLOY')
"""
        with c.open_sftp() as s2:
            with s2.file("/tmp/deploy_payfix.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/deploy_payfix.py", timeout=600)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1000])
        code = stdout.channel.recv_exit_status()
        if code != 0:
            return code

        # SQL catch-up Najaf with QUOTED_IDENTIFIER
        sql_remote = r'''
import json, subprocess
cfg=json.load(open('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json',encoding='utf-8'))
cs=cfg['ConnectionStrings']['DataBaseConnection']
parts={}
for p in cs.split(';'):
    if '=' in p:
        k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
sql="""
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
UPDATE dbo.CustomersPaymentsRequest
SET AutoPostEnabled = 1,
    CreatedAtUtc = ISNULL(CreatedAtUtc, CASE WHEN PaymentDate IS NOT NULL THEN CAST(PaymentDate AS DATETIME2) ELSE SYSUTCDATETIME() END),
    EligibleForPostingAtUtc = ISNULL(EligibleForPostingAtUtc, SYSUTCDATETIME()),
    UpdatedDate = GETDATE()
WHERE PostedToBoxAtUtc IS NULL
  AND EligibleForPostingAtUtc IS NULL;
PRINT CONCAT('FixedEligible=', @@ROWCOUNT);
DECLARE @PostedCount INT;
EXEC dbo.CustomersPaymentsRequest_PostEligible @UserCreateID=1, @PostedCount=@PostedCount OUTPUT;
PRINT CONCAT('PostedCount=', ISNULL(CAST(@PostedCount AS nvarchar(20)),'NULL'));
SELECT COUNT(*) AS UnpostedLeft FROM dbo.CustomersPaymentsRequest WHERE PostedToBoxAtUtc IS NULL AND CreatedDate >= DATEADD(day,-2,GETDATE());
SELECT COUNT(*) AS List5Today FROM dbo.CustomersPayments WHERE DelegateID=5 AND CONVERT(date,PaymentDate)='2026-09-21';
"""
open('/tmp/fix_el2.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-I','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/fix_el2.sql','-W','-s','|','-f','65001'],capture_output=True,text=True)
print(r.stdout)
print(r.stderr[:500] if r.stderr else '')
print('SQL_RC', r.returncode)
'''
        with c.open_sftp() as s3:
            with s3.file("/tmp/sql_payfix.py", "w") as f:
                f.write(sql_remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/sql_payfix.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("SQL_ERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
