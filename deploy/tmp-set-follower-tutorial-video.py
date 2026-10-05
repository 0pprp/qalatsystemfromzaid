#!/usr/bin/env python3
"""Upsert follower tutorial YouTube URL on production Sales Gateway."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
VIDEO_URL = "https://youtu.be/KtrCPWMogG4"
APP_KEY = "follower"
TITLE = "تطبيق المتابع"

REMOTE = r'''
import json, os, re, subprocess, urllib.request

VIDEO_URL = %r
APP_KEY = %r
TITLE = %r

def find_cs():
    for path in [
        '/etc/qalaa-production/sales-gateway.env',
        '/opt/qalaa-runtime/SalesGateway/appsettings.json',
    ]:
        if not os.path.isfile(path):
            continue
        txt = open(path, encoding='utf-8', errors='replace').read()
        if path.endswith('.json'):
            try:
                data = json.loads(txt)
                cs = (data.get('ConnectionStrings') or {}).get('SalesGateway') or ''
                if cs:
                    return cs, path
            except Exception:
                pass
        else:
            m = re.search(r'ConnectionStrings__SalesGateway=(.+)', txt)
            if m:
                return m.group(1).strip().strip('"'), path
    env = subprocess.check_output(
        ['systemctl', 'show', 'qalaa-sales-gateway', '-p', 'Environment', '--value'],
        text=True,
    )
    m = re.search(r'ConnectionStrings__SalesGateway=([^\s]+)', env)
    if m:
        return m.group(1), 'systemd'
    return '', ''

cs, src = find_cs()
print('CS_SRC', src, 'LEN', len(cs))
if not cs:
    raise SystemExit('no prod cs')

parts = {}
for p in cs.split(';'):
    if '=' in p:
        k, v = p.split('=', 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get('server') or parts.get('data source')
db = parts.get('database') or parts.get('initial catalog')
user = parts.get('user id') or parts.get('uid')
pwd = parts.get('password') or parts.get('pwd')
print('TARGET', server, db)

sql = f"""
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.MobileAppTutorialLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MobileAppTutorialLinks (
        AppKey NVARCHAR(64) NOT NULL CONSTRAINT PK_MobileAppTutorialLinks PRIMARY KEY,
        TitleAr NVARCHAR(200) NOT NULL,
        VideoUrl NVARCHAR(1024) NOT NULL CONSTRAINT DF_MATL_VideoUrl DEFAULT (N''),
        IsEnabled BIT NOT NULL CONSTRAINT DF_MATL_IsEnabled DEFAULT (1),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_MATL_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(128) NULL
    );
END

MERGE dbo.MobileAppTutorialLinks AS target
USING (SELECT N'{{APP_KEY}}' AS AppKey) AS source ON target.AppKey = source.AppKey
WHEN MATCHED THEN UPDATE SET
    TitleAr = N'{{TITLE}}',
    VideoUrl = N'{{VIDEO_URL}}',
    IsEnabled = 1,
    UpdatedAtUtc = SYSUTCDATETIME(),
    UpdatedBy = N'ops-upload'
WHEN NOT MATCHED THEN INSERT
    (AppKey, TitleAr, VideoUrl, IsEnabled, UpdatedAtUtc, UpdatedBy)
    VALUES
    (N'{{APP_KEY}}', N'{{TITLE}}', N'{{VIDEO_URL}}', 1, SYSUTCDATETIME(), N'ops-upload');

SELECT AppKey, TitleAr, VideoUrl, IsEnabled, UpdatedAtUtc, UpdatedBy
FROM dbo.MobileAppTutorialLinks
WHERE AppKey = N'{{APP_KEY}}';
""".replace('{APP_KEY}', APP_KEY).replace('{TITLE}', TITLE).replace('{VIDEO_URL}', VIDEO_URL)

open('/tmp/set_follower_tutorial.sql', 'w', encoding='utf-8').write(sql)
bin = '/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin):
    bin = 'sqlcmd'
cmd = [bin, '-S', server, '-d', db, '-C', '-I', '-f', '65001', '-y', '0', '-Y', '200',
       '-s', '|', '-i', '/tmp/set_follower_tutorial.sql', '-U', user, '-P', pwd]
r = subprocess.run(cmd, capture_output=True, text=True)
print(r.stdout or '')
if r.stderr:
    print('STDERR', r.stderr[-2000:])
print('SQL_EXIT', r.returncode)

# Verify public API (gateway local)
for base in (
    'http://127.0.0.1:5280/api/mobile-tutorial-links/' + APP_KEY,
    'http://127.0.0.1/api/mobile-tutorial-links/' + APP_KEY,
):
    try:
        with urllib.request.urlopen(base, timeout=12) as resp:
            body = resp.read().decode('utf-8', 'replace')
            print('API', base, resp.status, body[:400])
            break
    except Exception as ex:
        print('API_FAIL', base, ex)
''' % (VIDEO_URL, APP_KEY, TITLE)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    with c.open_sftp() as sftp:
        with sftp.file("/tmp/set_follower_tutorial.py", "w") as f:
            f.write(REMOTE)
    _, out, err = c.exec_command("python3 /tmp/set_follower_tutorial.py", timeout=120)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    PathOut = __import__("pathlib").Path
    PathOut(__file__).with_name("_follower_tutorial_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-800:])
    c.close()
    ok = "youtu.be/KtrCPWMogG4" in text and "SQL_EXIT 0" in text
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
