#!/usr/bin/env python3
"""Upsert branch-manager tutorial YouTube URL on production Sales Gateway."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
VIDEO_URL = "https://youtu.be/cdAtAIuyk7I"
APP_KEY = "branch-manager"
TITLE = "تطبيق مدير الفرع"

REMOTE = rf'''
import json, os, re, subprocess, urllib.request

VIDEO_URL = {VIDEO_URL!r}
APP_KEY = {APP_KEY!r}
TITLE = {TITLE!r}

text=open('/etc/qalaa-production/delegated-manager.env',encoding='utf-8',errors='replace').read()
cs=None
for line in text.splitlines():
    if line.startswith('ConnectionStrings__SalesGateway='):
        cs=line.split('=',1)[1].strip().strip('"').strip("'")
if not cs:
    raise SystemExit('no cs')
parts={{}}
for p in cs.split(';'):
    if '=' in p:
        k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
server=parts.get('server') or parts.get('data source')
db=parts.get('initial catalog') or parts.get('database')
user=parts.get('user id') or parts.get('uid')
pwd=parts.get('password') or parts.get('pwd')

sql=f"""
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
SELECT AppKey, TitleAr, VideoUrl, IsEnabled FROM dbo.MobileAppTutorialLinks WHERE AppKey = N'{{APP_KEY}}';
""".replace('{{APP_KEY}}', APP_KEY).replace('{{TITLE}}', TITLE).replace('{{VIDEO_URL}}', VIDEO_URL)

open('/tmp/set_bm_tutorial.sql','w',encoding='utf-8').write(sql)
r=subprocess.run(['/opt/mssql-tools18/bin/sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i','/tmp/set_bm_tutorial.sql'],capture_output=True,text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-1500:])
print('SQL_EXIT', r.returncode)
with urllib.request.urlopen(f'http://127.0.0.1:5280/api/mobile-tutorial-links/{{APP_KEY}}', timeout=12) as resp:
    print('API_OK', resp.read().decode('utf-8','replace')[:400])
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    with c.open_sftp() as sftp:
        with sftp.file("/tmp/set_bm_tutorial.py", "w") as f:
            f.write(REMOTE)
    _, out, err = c.exec_command("python3 /tmp/set_bm_tutorial.py", timeout=120)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_bm_tutorial_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-800:])
    c.close()
    return 0 if "cdAtAIuyk7I" in text and "SQL_EXIT 0" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
