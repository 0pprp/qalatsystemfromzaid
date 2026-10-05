#!/usr/bin/env python3
"""Disable mandatory update for delegate app — no new APK publish."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
APP_KEY = "delegate"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        inner = f"""
import subprocess, json, urllib.request
text=open('/etc/qalaa-production/delegated-manager.env',encoding='utf-8',errors='replace').read()
cs=None
for line in text.splitlines():
    if line.startswith('ConnectionStrings__SalesGateway='):
        cs=line.split('=',1)[1].strip().strip('"').strip("'")
parts={{}}
for part in cs.split(';'):
    if '=' in part:
        k,v=part.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')
sql='''SET NOCOUNT ON;
UPDATE dbo.MobileAppReleases
SET ForceUpdate = 0,
    MinimumSupportedVersionCode = 1,
    ReleaseNotes = N'التحديث الاختياري متاح لاحقاً — يمكن متابعة العمل بالنسخة الحالية.',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AppKey = N'{APP_KEY}';
SELECT AppKey, LatestVersionName, LatestVersionCode, ForceUpdate, MinimumSupportedVersionCode, ReleaseNotes
FROM dbo.MobileAppReleases WHERE AppKey = N'{APP_KEY}';
'''
open('/tmp/mu_delegate_disable.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/mu_delegate_disable.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout); print('RC', r.returncode)
for code in (20, 23, 24):
    with urllib.request.urlopen(f'http://127.0.0.1:5280/api/mobile-updates/{APP_KEY}?currentVersionCode={{code}}',timeout=20) as resp:
        data=json.loads(resp.read().decode())
        print(f'UPDATE code={{code}}', {{k:data.get(k) for k in ('latestVersionName','latestVersionCode','forceUpdate','updateAvailable','mandatory','updateKind','minimumSupportedVersionCode')}})
print('DONE')
"""
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/disable_delegate_mandatory.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/disable_delegate_mandatory.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
