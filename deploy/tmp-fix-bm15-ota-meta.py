#!/usr/bin/env python3
"""Fix MobileAppReleases row for branch-manager 1.0.14+15 (APK already uploaded)."""
from __future__ import annotations

import hashlib
import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
APK = ROOT / "branch_manager_application/build/app/outputs/flutter-apk/app-release.apk"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not APK.exists():
        print("APK missing")
        return 2
    sha = hashlib.sha256(APK.read_bytes()).hexdigest()

    remote = f"""
import json, subprocess, urllib.request
text = open('/etc/qalaa-production/delegated-manager.env', encoding='utf-8', errors='replace').read()
cs = None
for line in text.splitlines():
    if line.startswith('ConnectionStrings__SalesGateway='):
        cs = line.split('=', 1)[1].strip().strip('"').strip("'")
parts = {{}}
for part in cs.split(';'):
    if '=' in part:
        k, v = part.split('=', 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get('server') or parts.get('data source')
db = parts.get('initial catalog') or parts.get('database')
user = parts.get('user id') or parts.get('uid')
pwd = parts.get('password') or parts.get('pwd')
sha = {sha!r}
sql = (
    "SET NOCOUNT ON;\\n"
    "MERGE dbo.MobileAppReleases AS t\\n"
    "USING (SELECT N'branch-manager' AS AppKey) AS s ON t.AppKey=s.AppKey\\n"
    "WHEN MATCHED THEN UPDATE SET\\n"
    " LatestVersionName=N'1.0.14', LatestVersionCode=15,\\n"
    " MinimumSupportedVersionCode=1,\\n"
    " ForceUpdate=0,\\n"
    " ApkUrl=N'http://admincompany.alsaaeidy.com/mobile-updates/branch-manager/branch-manager-15.apk',\\n"
    f" Sha256=N'{{sha}}',\\n"
    " ReleaseNotes=N'لوحة القرارات: إخفاء المصفرين (الباقي 0).',\\n"
    " UpdatedAtUtc=SYSUTCDATETIME()\\n"
    "WHEN NOT MATCHED THEN INSERT\\n"
    " (AppKey,LatestVersionName,LatestVersionCode,MinimumSupportedVersionCode,ForceUpdate,ApkUrl,Sha256,ReleaseNotes,UpdatedAtUtc)\\n"
    " VALUES (N'branch-manager',N'1.0.14',15,1,0,\\n"
    " N'http://admincompany.alsaaeidy.com/mobile-updates/branch-manager/branch-manager-15.apk',\\n"
    f" N'{{sha}}',\\n"
    " N'لوحة القرارات: إخفاء المصفرين (الباقي 0).',\\n"
    " SYSUTCDATETIME());\\n"
    "SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate FROM dbo.MobileAppReleases WHERE AppKey=N'branch-manager';\\n"
)
open('/tmp/mu_bm15.sql', 'w', encoding='utf-8').write(sql)
r = subprocess.run(
    ['/opt/mssql-tools18/bin/sqlcmd', '-C', '-S', server, '-d', db, '-U', user, '-P', pwd, '-i', '/tmp/mu_bm15.sql', '-W', '-s', '|'],
    capture_output=True, text=True,
)
print(r.stdout)
print(r.stderr[:400] if r.stderr else '')
print('RC', r.returncode)
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/branch-manager?currentVersionCode=14', timeout=20) as resp:
    print('UPDATE', json.loads(resp.read().decode()))
print('DONE')
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/fix_bm15_meta.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/fix_bm15_meta.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
