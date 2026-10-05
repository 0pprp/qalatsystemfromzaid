#!/usr/bin/env python3
"""Publish optional tutorial-button APKs for 4 apps (not delegate)."""
from __future__ import annotations

import hashlib
import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent

APPS = [
    {
        "app_key": "follower",
        "version_name": "1.0.14",
        "version_code": 15,
        "apk": ROOT / "follower_application/build/app/outputs/flutter-apk/app-release.apk",
        "remote_dir": "/opt/qalaa-updates/follower",
        "file_stem": "follower",
        "notes": "تحديث اختياري: زر مشاهدة الفيديو التعليمي من الرابط الذي يضعه مدير المبيعات.",
    },
    {
        "app_key": "main-follower",
        "version_name": "1.0.5",
        "version_code": 6,
        "apk": ROOT / "main_follower_application/build/app/outputs/flutter-apk/app-release.apk",
        "remote_dir": "/opt/qalaa-updates/main-follower",
        "file_stem": "main-follower",
        "notes": "تحديث اختياري: الفيديو التعليمي من قائمة المهام (الرابط من لوحة مدير المبيعات).",
    },
    {
        "app_key": "delegated-manager",
        "version_name": "1.0.12",
        "version_code": 13,
        "apk": ROOT / "delegated_manager_application/build/app/outputs/flutter-apk/app-release.apk",
        "remote_dir": "/opt/qalaa-updates/delegated-manager",
        "file_stem": "delegated-manager",
        "notes": "تحديث اختياري: الفيديو التعليمي من الإعدادات (الرابط من لوحة مدير المبيعات).",
    },
    {
        "app_key": "branch-manager",
        "version_name": "1.0.9",
        "version_code": 10,
        "apk": ROOT / "branch_manager_application/build/app/outputs/flutter-apk/app-release.apk",
        "remote_dir": "/opt/qalaa-updates/branch-manager",
        "file_stem": "branch-manager",
        "notes": "تحديث اختياري: الفيديو التعليمي من القائمة الجانبية (الرابط من لوحة مدير المبيعات).",
    },
]


def publish_one(c: paramiko.SSHClient, app: dict) -> int:
    apk: Path = app["apk"]
    if not apk.exists():
        print("APK missing", apk)
        return 2
    version_code = app["version_code"]
    version_name = app["version_name"]
    app_key = app["app_key"]
    remote_dir = app["remote_dir"]
    stem = app["file_stem"]
    remote_apk = f"{remote_dir}/{stem}-{version_code}.apk"
    apk_url = f"http://admincompany.alsaaeidy.com/mobile-updates/{app_key}/{stem}-{version_code}.apk"
    notes_sql = app["notes"].replace("'", "''")
    sha = hashlib.sha256(apk.read_bytes()).hexdigest()
    print("LOCAL", app_key, apk.stat().st_size, sha, flush=True)

    c.exec_command(f"mkdir -p {remote_dir}")[1].channel.recv_exit_status()
    sftp = c.open_sftp()
    sftp.put(str(apk), remote_apk)
    sftp.close()

    inner = f"""
import subprocess, json, urllib.request
subprocess.run([
  'bash','-lc',
  'mkdir -p {remote_dir}; ln -sfn {remote_apk} {remote_dir}/{stem}-latest.apk; '
  'ln -sfn {remote_dir} /opt/qalaa-runtime/AdminFrontend/mobile-updates/{app_key}; '
  'ln -sfn {remote_dir} /var/www/html/mobile-updates/{app_key}; chmod 644 {remote_apk}'
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
USING (SELECT N'{app_key}' AS AppKey) AS s ON t.AppKey=s.AppKey
WHEN MATCHED THEN UPDATE SET
 LatestVersionName=N'{version_name}', LatestVersionCode={version_code},
 MinimumSupportedVersionCode=1,
 ForceUpdate=0, ApkUrl=N'{apk_url}', Sha256=N'{{sha}}',
 ReleaseNotes=N'{notes_sql}', UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
 (AppKey,LatestVersionName,LatestVersionCode,MinimumSupportedVersionCode,ForceUpdate,ApkUrl,Sha256,ReleaseNotes,UpdatedAtUtc)
 VALUES (N'{app_key}',N'{version_name}',{version_code},1,0,N'{apk_url}',N'{{sha}}',N'{notes_sql}',SYSUTCDATETIME());
SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate,MinimumSupportedVersionCode FROM dbo.MobileAppReleases WHERE AppKey=N'{app_key}';
'''
open('/tmp/mu_tutorial_{stem}.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/mu_tutorial_{stem}.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout); print('RC', r.returncode)
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/{app_key}?currentVersionCode={version_code - 1}',timeout=20) as resp:
    print('UPDATE', json.loads(resp.read().decode()))
req=urllib.request.Request('http://127.0.0.1/mobile-updates/{app_key}/{stem}-{version_code}.apk', headers={{'Host':'admincompany.alsaaeidy.com'}}, method='HEAD')
with urllib.request.urlopen(req, timeout=20) as resp:
    print('APK_HTTP', resp.status, resp.headers.get('Content-Length'))
print('DONE', '{app_key}')
"""
    remote_py = f"/tmp/pub_tutorial_{stem}.py"
    with c.open_sftp() as sftp2:
        with sftp2.file(remote_py, "w") as f:
            f.write(inner)
    stdin, stdout, stderr = c.exec_command(f"python3 {remote_py}", timeout=180)
    print(stdout.read().decode("utf-8", "replace"))
    err = stderr.read().decode("utf-8", "replace")
    if err.strip():
        print("STDERR", err[:500])
    return stdout.channel.recv_exit_status()


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    only = [a.strip() for a in os.environ.get("ONLY_APPS", "").split(",") if a.strip()]
    apps = [a for a in APPS if not only or a["app_key"] in only]
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        rc = 0
        for app in apps:
            print("=" * 60, flush=True)
            code = publish_one(c, app)
            if code:
                rc = code
                print("FAILED", app["app_key"], code)
        return rc
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
