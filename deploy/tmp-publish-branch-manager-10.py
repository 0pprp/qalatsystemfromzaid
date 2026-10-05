#!/usr/bin/env python3
"""Publish branch-manager 1.0.9+10 tutorial APK only."""
from __future__ import annotations

import hashlib
import os
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
APK = ROOT / "branch_manager_application/build/app/outputs/flutter-apk/app-release.apk"
REMOTE = "/opt/qalaa-updates/branch-manager/branch-manager-10.apk"
APK_URL = "http://admincompany.alsaaeidy.com/mobile-updates/branch-manager/branch-manager-10.apk"
NOTES = "تحديث اختياري: الفيديو التعليمي من القائمة الجانبية (الرابط من لوحة مدير المبيعات)."


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not APK.exists():
        print("APK missing", APK)
        return 2
    sha = hashlib.sha256(APK.read_bytes()).hexdigest()
    print("size", APK.stat().st_size, "sha", sha, flush=True)
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        banner_timeout=60,
        auth_timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    try:
        print("connected", flush=True)
        c.exec_command("mkdir -p /opt/qalaa-updates/branch-manager")[1].channel.recv_exit_status()
        t0 = time.time()

        def cb(transferred: int, total: int) -> None:
            if transferred == total or transferred % (5 * 1024 * 1024) < 32768:
                print(f"up {transferred}/{total} {time.time() - t0:.1f}s", flush=True)

        sftp = c.open_sftp()
        sftp.get_channel().settimeout(600)
        sftp.put(str(APK), REMOTE, callback=cb)
        sftp.close()
        print("uploaded", flush=True)

        notes_sql = NOTES.replace("'", "''")
        inner = f"""
import subprocess, json, urllib.request
subprocess.run([
  'bash','-lc',
  'ln -sfn {REMOTE} /opt/qalaa-updates/branch-manager/branch-manager-latest.apk; '
  'ln -sfn /opt/qalaa-updates/branch-manager /opt/qalaa-runtime/AdminFrontend/mobile-updates/branch-manager; '
  'ln -sfn /opt/qalaa-updates/branch-manager /var/www/html/mobile-updates/branch-manager; '
  'chmod 644 {REMOTE}'
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
USING (SELECT N'branch-manager' AS AppKey) AS s ON t.AppKey=s.AppKey
WHEN MATCHED THEN UPDATE SET
 LatestVersionName=N'1.0.9', LatestVersionCode=10,
 MinimumSupportedVersionCode=1,
 ForceUpdate=0, ApkUrl=N'{APK_URL}', Sha256=N'{{sha}}',
 ReleaseNotes=N'{notes_sql}', UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
 (AppKey,LatestVersionName,LatestVersionCode,MinimumSupportedVersionCode,ForceUpdate,ApkUrl,Sha256,ReleaseNotes,UpdatedAtUtc)
 VALUES (N'branch-manager',N'1.0.9',10,1,0,N'{APK_URL}',N'{{sha}}',N'{notes_sql}',SYSUTCDATETIME());
SELECT AppKey,LatestVersionName,LatestVersionCode,ForceUpdate FROM dbo.MobileAppReleases WHERE AppKey=N'branch-manager';
'''
open('/tmp/mu_bm10.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-i','/tmp/mu_bm10.sql','-W','-s','|'],capture_output=True,text=True)
print(r.stdout); print('RC', r.returncode)
with urllib.request.urlopen('http://127.0.0.1:5280/api/mobile-updates/branch-manager?currentVersionCode=9',timeout=20) as resp:
    print('UPDATE', json.loads(resp.read().decode()))
req=urllib.request.Request('http://127.0.0.1/mobile-updates/branch-manager/branch-manager-10.apk', headers={{'Host':'admincompany.alsaaeidy.com'}}, method='HEAD')
with urllib.request.urlopen(req, timeout=20) as resp:
    print('APK_HTTP', resp.status, resp.headers.get('Content-Length'))
print('DONE')
"""
        with c.open_sftp() as sftp2:
            with sftp2.file("/tmp/pub_bm10.py", "w") as f:
                f.write(inner)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/pub_bm10.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
