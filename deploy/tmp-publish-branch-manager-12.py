#!/usr/bin/env python3
"""Publish branch-manager APK 1.0.11+12 — تقييم بدون نسبة + أسماء + بروفايل."""
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
VERSION_CODE = 12
VERSION_NAME = "1.0.11"
MIN_CODE = 1
FORCE_UPDATE = 0
APP_KEY = "branch-manager"
REMOTE_DIR = "/opt/qalaa-updates/branch-manager"
REMOTE_APK = f"{REMOTE_DIR}/branch-manager-{VERSION_CODE}.apk"
APK_URL = (
    f"http://admincompany.alsaaeidy.com/mobile-updates/branch-manager/branch-manager-{VERSION_CODE}.apk"
)
NOTES = (
    "تحديث التقييم: بدون نسب مئوية، تصميم نقاط مثل لوحة المدير، "
    "اضغط الحالة/الأسماء لعرض القائمة ثم الاسم لفتح البروفايل."
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
import subprocess, json, urllib.request
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
 LatestVersionName=N'{VERSION_NAME}', LatestVersionCode={VERSION_CODE},
 MinimumSupportedVersionCode={MIN_CODE},
 ForceUpdate={FORCE_UPDATE}, ApkUrl=N'{APK_URL}', Sha256=N'{{sha}}',
 ReleaseNotes=N'{notes_sql}', UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
 (AppKey,LatestVersionName,LatestVersionCode,MinimumSupportedVersionCode,ForceUpdate,ApkUrl,Sha256,ReleaseNotes,UpdatedAtUtc)
 VALUES (N'{APP_KEY}',N'{VERSION_NAME}',{VERSION_CODE},{MIN_CODE},{FORCE_UPDATE},N'{APK_URL}',N'{{sha}}',N'{notes_sql}',SYSUTCDATETIME());
SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate FROM dbo.MobileAppReleases WHERE AppKey=N'{APP_KEY}';
'''
open('/tmp/mu_bm12.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/mu_bm12.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout); print('RC', r.returncode)
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/{APP_KEY}?currentVersionCode=11',timeout=20) as resp:
    print('UPDATE', json.loads(resp.read().decode()))
req=urllib.request.Request('http://127.0.0.1/mobile-updates/{APP_KEY}/branch-manager-{VERSION_CODE}.apk', headers={{'Host':'admincompany.alsaaeidy.com'}}, method='HEAD')
with urllib.request.urlopen(req, timeout=20) as resp:
    print('APK_HTTP', resp.status, resp.headers.get('Content-Length'))
print('DONE')
"""
        with c.open_sftp() as sftp2:
            with sftp2.file("/tmp/pub_bm12.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command(
            f"mkdir -p {REMOTE_DIR}; python3 /tmp/pub_bm12.py", timeout=120
        )
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write(
            (out + ("\nSTDERR " + err[:500] if err.strip() else "") + "\n").encode(
                "utf-8", "replace"
            )
        )
        code = stdout.channel.recv_exit_status()
        print("EXIT", code, APK_URL)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
