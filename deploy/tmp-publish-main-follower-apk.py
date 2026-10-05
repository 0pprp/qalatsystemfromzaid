#!/usr/bin/env python3
"""Upload main-follower demo APK and register MobileAppReleases (v1.0.3+4)."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
APK = ROOT / "main_follower_demo.apk"
if not APK.exists():
    APK = ROOT / "main_follower_application/build/app/outputs/flutter-apk/app-release.apk"

VERSION_CODE = 5
VERSION_NAME = "1.0.4"
MIN_CODE = 1
FORCE_UPDATE = 0
APP_KEY = "main-follower"
REMOTE_DIR = "/opt/qalaa-updates/main-follower"
REMOTE_APK = f"{REMOTE_DIR}/main-follower-{VERSION_CODE}.apk"
APK_URL = (
    f"http://admincompany.alsaaeidy.com/mobile-updates/main-follower/"
    f"main-follower-{VERSION_CODE}.apk"
)
NOTES = "مصداقة المبيع مثل المتابع الفرعي + ملاحظة إجبارية عند ≤6 + إصلاح تحميل المستمسكات."


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not APK.exists():
        print("APK missing", APK)
        return 2

    sha = hashlib.sha256(APK.read_bytes()).hexdigest()
    print("LOCAL", APK, APK.stat().st_size, sha, flush=True)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        c.exec_command(f"mkdir -p {REMOTE_DIR}")[1].channel.recv_exit_status()
        print("sftp put...", flush=True)
        sftp = c.open_sftp()
        sftp.put(str(APK), REMOTE_APK)
        sftp.close()
        print("uploaded", flush=True)

        notes_sql = NOTES.replace("'", "''")
        inner = f"""
import json, os, re, subprocess, urllib.request, urllib.error

REMOTE_DIR={REMOTE_DIR!r}
REMOTE_APK={REMOTE_APK!r}
APP_KEY={APP_KEY!r}
VERSION_CODE={VERSION_CODE}
VERSION_NAME={VERSION_NAME!r}
MIN_CODE={MIN_CODE}
FORCE_UPDATE={FORCE_UPDATE}
APK_URL={APK_URL!r}
sha={sha!r}
notes_sql={notes_sql!r}

subprocess.run([
  'bash','-lc',
  f'mkdir -p {{REMOTE_DIR}}; '
  f'ln -sfn {{REMOTE_APK}} {{REMOTE_DIR}}/main-follower-latest.apk; '
  f'mkdir -p /opt/qalaa-runtime/AdminFrontend/mobile-updates; '
  f'ln -sfn {{REMOTE_DIR}} /opt/qalaa-runtime/AdminFrontend/mobile-updates/main-follower; '
  f'mkdir -p /var/www/html/mobile-updates; '
  f'ln -sfn {{REMOTE_DIR}} /var/www/html/mobile-updates/main-follower; '
  f'chmod 644 {{REMOTE_APK}}'
], check=False)

sql=f'''SET NOCOUNT ON;
MERGE dbo.MobileAppReleases AS t
USING (SELECT N'{{APP_KEY}}' AS AppKey) AS s ON t.AppKey=s.AppKey
WHEN MATCHED THEN UPDATE SET
 LatestVersionName=N'{{VERSION_NAME}}', LatestVersionCode={{VERSION_CODE}},
 MinimumSupportedVersionCode={{MIN_CODE}},
 ForceUpdate={{FORCE_UPDATE}}, ApkUrl=N'{{APK_URL}}', Sha256=N'{{sha}}',
 ReleaseNotes=N'{{notes_sql}}', UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
 (AppKey,LatestVersionName,LatestVersionCode,MinimumSupportedVersionCode,ForceUpdate,ApkUrl,Sha256,ReleaseNotes,UpdatedAtUtc)
 VALUES (N'{{APP_KEY}}',N'{{VERSION_NAME}}',{{VERSION_CODE}},{{MIN_CODE}},{{FORCE_UPDATE}},N'{{APK_URL}}',N'{{sha}}',N'{{notes_sql}}',SYSUTCDATETIME());
SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate,ApkUrl FROM dbo.MobileAppReleases WHERE AppKey=N'{{APP_KEY}}';
'''
open('/tmp/mu_main_follower.sql','w',encoding='utf-8').write(sql)

def parse_cs(text):
    for line in text.splitlines():
        if line.startswith('ConnectionStrings__SalesGateway='):
            return line.split('=',1)[1].strip().strip('"').strip("'")
    return None

def parts_of(cs):
    parts={{}}
    for part in cs.split(';'):
        if '=' in part:
            k,v=part.split('=',1); parts[k.strip().lower()]=v.strip()
    return (
        parts.get('server') or parts.get('data source'),
        parts.get('initial catalog') or parts.get('database'),
        parts.get('user id') or parts.get('uid'),
        parts.get('password') or parts.get('pwd'),
    )

def run_sql(label, cs):
    if not cs:
        print(label, 'NO_CS'); return
    server, db, user, pwd = parts_of(cs)
    print(label, 'DB', server, db)
    r=subprocess.run(
        ['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,
         '-i','/tmp/mu_main_follower.sql','-W','-s','|'],
        capture_output=True, text=True)
    print(label, r.stdout[-800:])
    print(label, 'RC', r.returncode)

# production CS (same as delegate publish)
for path in [
  '/etc/qalaa-production/delegated-manager.env',
  '/etc/qalaa-production/sales-gateway.env',
  '/etc/qalaa-demo/sales-gateway.env',
]:
    if os.path.isfile(path):
        cs=parse_cs(open(path,encoding='utf-8',errors='replace').read())
        run_sql(path, cs)

# verify download + API
req=urllib.request.Request(
  f'http://127.0.0.1/mobile-updates/{{APP_KEY}}/main-follower-{{VERSION_CODE}}.apk',
  headers={{'Host':'admincompany.alsaaeidy.com'}}, method='HEAD')
try:
    with urllib.request.urlopen(req, timeout=20) as resp:
        print('APK_HTTP', resp.status, resp.headers.get('Content-Length'))
except Exception as e:
    print('APK_HTTP_FAIL', e)

for base in ['http://127.0.0.1:5403/api','http://127.0.0.1:5280/api']:
    try:
        with urllib.request.urlopen(base+f'/mobile-updates/{{APP_KEY}}?currentVersionCode=0', timeout=20) as resp:
            body=json.loads(resp.read().decode())
        print('UPDATE', base, body.get('latestVersionCode'), body.get('apkUrl'), body.get('updateKind'))
    except Exception as e:
        print('UPDATE_FAIL', base, e)
print('DONE', APK_URL)
"""
        with c.open_sftp() as s2:
            with s2.file("/tmp/pub_main_follower.py", "w") as f:
                f.write(inner)
        i, o, e = c.exec_command("python3 /tmp/pub_main_follower.py", timeout=300)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        code = o.channel.recv_exit_status()
        print("EXIT", code)
        print(APK_URL)
        return code
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
