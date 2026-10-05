#!/usr/bin/env python3
"""Publish branch-manager APK 1.0.14+15 — hide settled (remaining=0) from decisions."""
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
VERSION_CODE = 15
VERSION_NAME = "1.0.14"
MIN_CODE = 1
FORCE_UPDATE = 0
APP_KEY = "branch-manager"
REMOTE_DIR = "/opt/qalaa-updates/branch-manager"
REMOTE_APK = f"{REMOTE_DIR}/branch-manager-{VERSION_CODE}.apk"
APK_URL = (
    f"http://admincompany.alsaaeidy.com/mobile-updates/branch-manager/branch-manager-{VERSION_CODE}.apk"
)
NOTES = (
    "لوحة القرارات: إخفاء المصفرين (الباقي 0) — لا قرار مطلوب عليهم."
)


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not APK.exists():
        print("APK missing", APK)
        return 2

    sha = hashlib.sha256(APK.read_bytes()).hexdigest()
    print("LOCAL", APK.stat().st_size, sha, flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False
    )
    try:
        print("Uploading APK...", flush=True)
        c.exec_command(f"mkdir -p {REMOTE_DIR}")[1].channel.recv_exit_status()
        sftp = c.open_sftp()
        sftp.put(str(APK), REMOTE_APK)
        sftp.close()

        notes_sql = NOTES.replace("'", "''")
        inner = f"""
import subprocess
subprocess.run([
  'bash','-lc',
  'mkdir -p {REMOTE_DIR}; ln -sfn {REMOTE_APK} {REMOTE_DIR}/branch-manager-latest.apk; '
  'ln -sfn {REMOTE_DIR} /opt/qalaa-runtime/AdminFrontend/mobile-updates/branch-manager; '
  'ln -sfn {REMOTE_DIR} /var/www/html/mobile-updates/branch-manager; chmod 644 {REMOTE_APK}; ls -la {REMOTE_APK}'
], check=False)
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
sha={sha!r}
sql=f'''SET NOCOUNT ON;
MERGE dbo.MobileAppReleases AS t
USING (SELECT N'{APP_KEY}' AS AppKey) AS s ON t.AppKey=s.AppKey
WHEN MATCHED THEN UPDATE SET
  VersionCode={VERSION_CODE}, VersionName=N'{VERSION_NAME}', MinSupportedVersionCode={MIN_CODE},
  ApkUrl=N'{APK_URL}', Sha256=N'{{sha}}', ReleaseNotes=N'{notes_sql}',
  ForceUpdate={FORCE_UPDATE}, IsActive=1, PublishedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
  (AppKey, VersionCode, VersionName, MinSupportedVersionCode, ApkUrl, Sha256, ReleaseNotes, ForceUpdate, IsActive, PublishedAtUtc)
  VALUES (N'{APP_KEY}', {VERSION_CODE}, N'{VERSION_NAME}', {MIN_CODE}, N'{APK_URL}', N'{{sha}}', N'{notes_sql}', {FORCE_UPDATE}, 1, SYSUTCDATETIME());
SELECT AppKey, VersionCode, VersionName FROM dbo.MobileAppReleases WHERE AppKey=N'{APP_KEY}';'''
import subprocess as sp
r=sp.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-I','-S',server,'-d',db,'-U',user,'-P',pwd,'-Q',sql],capture_output=True,text=True)
print(r.stdout)
print(r.stderr[:400] if r.stderr else '')
print('SQL_RC', r.returncode)
raise SystemExit(r.returncode)
"""
        sftp = c.open_sftp()
        with sftp.file("/tmp/publish_bm_15.py", "w") as f:
            f.write(inner)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/publish_bm_15.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
