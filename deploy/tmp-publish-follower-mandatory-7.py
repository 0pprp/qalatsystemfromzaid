#!/usr/bin/env python3
"""Publish follower APK 1.0.6+7 as MANDATORY update with native install fix."""
from __future__ import annotations

import hashlib
import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
APK = ROOT / "follower_application/build/app/outputs/flutter-apk/app-release.apk"
VERSION_CODE = 7
VERSION_NAME = "1.0.6"
MIN_CODE = 5
FORCE_UPDATE = 1
REMOTE_DIR = "/opt/qalaa-updates/follower"
REMOTE_APK = f"{REMOTE_DIR}/follower-{VERSION_CODE}.apk"
APK_URL = (
    f"http://admincompany.alsaaeidy.com/mobile-updates/follower/follower-{VERSION_CODE}.apk"
)
NOTES = (
    "تحديث إجباري: إصلاح فتح مثبت النظام، طلب إذن التثبيت، "
    "ورابط تنزيل من المتصفح إذا فشل التثبيت من داخل التطبيق."
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
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    try:
        print("Uploading APK...", flush=True)
        sftp = c.open_sftp()
        sftp.put(str(APK), REMOTE_APK)
        sftp.close()

        notes_sql = NOTES.replace("'", "''")
        inner = f"""
import subprocess, json, urllib.request
subprocess.run([
  'bash','-lc',
  'mkdir -p {REMOTE_DIR}; '
  'ln -sfn {REMOTE_APK} {REMOTE_DIR}/follower-latest.apk; '
  'ln -sfn {REMOTE_DIR} /opt/qalaa-runtime/AdminFrontend/mobile-updates/follower; '
  'ln -sfn {REMOTE_DIR} /var/www/html/mobile-updates/follower; '
  'chmod 644 {REMOTE_APK}; ls -la {REMOTE_APK}'
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
USING (SELECT N'follower' AS AppKey) AS s ON t.AppKey=s.AppKey
WHEN MATCHED THEN UPDATE SET
 LatestVersionName=N'{VERSION_NAME}', LatestVersionCode={VERSION_CODE},
 MinimumSupportedVersionCode={MIN_CODE},
 ForceUpdate={FORCE_UPDATE}, ApkUrl=N'{APK_URL}', Sha256=N'{{sha}}',
 ReleaseNotes=N'{notes_sql}', UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
 (AppKey,LatestVersionName,LatestVersionCode,MinimumSupportedVersionCode,ForceUpdate,ApkUrl,Sha256,ReleaseNotes,UpdatedAtUtc)
 VALUES (N'follower',N'{VERSION_NAME}',{VERSION_CODE},{MIN_CODE},{FORCE_UPDATE},N'{APK_URL}',N'{{sha}}',N'{notes_sql}',SYSUTCDATETIME());
SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate,ApkUrl
FROM dbo.MobileAppReleases WHERE AppKey=N'follower';
'''
open('/tmp/mu7.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/mu7.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout); print('RC', r.returncode)
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/follower?currentVersionCode=4',timeout=20) as resp:
    print('UPDATE_FROM_4', json.loads(resp.read().decode()))
req=urllib.request.Request('http://127.0.0.1/mobile-updates/follower/follower-{VERSION_CODE}.apk', headers={{'Host':'admincompany.alsaaeidy.com'}}, method='HEAD')
with urllib.request.urlopen(req, timeout=20) as resp:
    print('APK_HTTP', resp.status, resp.headers.get('Content-Length'))
print('DONE')
"""
        with c.open_sftp() as sftp2:
            with sftp2.file("/tmp/pub_follower7.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/pub_follower7.py", timeout=120)
        sys.stdout.write(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:500])
        code = stdout.channel.recv_exit_status()
        print("EXIT", code, APK_URL)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
