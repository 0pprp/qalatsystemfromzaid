#!/usr/bin/env python3
"""Finish mandatory register for already-uploaded delegate-27.apk."""
from __future__ import annotations

import hashlib
import os
import sys
from pathlib import Path

import paramiko

# Force UTF-8 console on Windows
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
APK = ROOT / "delegate_application/build/app/outputs/flutter-apk/app-release.apk"
VERSION_CODE = 27
VERSION_NAME = "1.0.8"
MIN_CODE = 27
FORCE_UPDATE = 1
APP_KEY = "delegate"
REMOTE_DIR = "/opt/qalaa-updates/delegate"
REMOTE_APK = f"{REMOTE_DIR}/delegate-{VERSION_CODE}.apk"
APK_URL = (
    f"http://admincompany.alsaaeidy.com/mobile-updates/delegate/delegate-{VERSION_CODE}.apk"
)
NOTES = (
    "تحديث إجباري: عمل المندوب اليومي (يوم من 3 فجراً، إرسال بعد 4 عصراً حتى 3 فجراً)، "
    "منع تضخيم أيام سابقة، شريط المسددين/غير المسددين من الذاكرة المحلية، "
    "وقسم المبيعات الجديدة فوق قائمة العملاء."
)
OUT = ROOT / "dist" / "_publish_delegate27_result.txt"


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not APK.exists():
        print("APK missing", APK)
        return 2
    sha = hashlib.sha256(APK.read_bytes()).hexdigest()
    notes_sql = NOTES.replace("'", "''")
    inner = f"""
import subprocess, json, urllib.request
subprocess.run([
  'bash','-lc',
  'mkdir -p {REMOTE_DIR}; ln -sfn {REMOTE_APK} {REMOTE_DIR}/delegate-latest.apk; '
  'ln -sfn {REMOTE_DIR} /opt/qalaa-runtime/AdminFrontend/mobile-updates/delegate; '
  'ln -sfn {REMOTE_DIR} /var/www/html/mobile-updates/delegate; chmod 644 {REMOTE_APK}'
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
SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate,MinimumSupportedVersionCode FROM dbo.MobileAppReleases WHERE AppKey=N'{APP_KEY}';
'''
open('/tmp/mu_delegate27.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/mu_delegate27.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout); print('RC', r.returncode)
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/{APP_KEY}?currentVersionCode=26',timeout=20) as resp:
    print('UPDATE', json.loads(resp.read().decode()))
req=urllib.request.Request('http://127.0.0.1/mobile-updates/{APP_KEY}/delegate-{VERSION_CODE}.apk', headers={{'Host':'admincompany.alsaaeidy.com'}}, method='HEAD')
with urllib.request.urlopen(req, timeout=20) as resp:
    print('APK_HTTP', resp.status, resp.headers.get('Content-Length'))
print('DONE')
"""
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
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/pub_delegate27_reg.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command(
            "python3 /tmp/pub_delegate27_reg.py", timeout=180
        )
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        code = stdout.channel.recv_exit_status()
        OUT.parent.mkdir(parents=True, exist_ok=True)
        OUT.write_text(out + (("\nSTDERR\n" + err) if err.strip() else ""), encoding="utf-8")
        print(out)
        if err.strip():
            print("STDERR", err[:500])
        print("EXIT", code, APK_URL)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
