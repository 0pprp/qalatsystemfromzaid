#!/usr/bin/env python3
"""Upsert follower tutorial URL — robust CS discovery."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
VIDEO_URL = "https://youtu.be/KtrCPWMogG4"

REMOTE = r'''
import json, os, re, subprocess, urllib.request

VIDEO_URL = "https://youtu.be/KtrCPWMogG4"
APP_KEY = "follower"
TITLE = "تطبيق المتابع"

def parse_cs(cs):
    parts={}
    for p in (cs or '').split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return (parts.get('server') or parts.get('data source'),
            parts.get('initial catalog') or parts.get('database'),
            parts.get('user id') or parts.get('uid'),
            parts.get('password') or parts.get('pwd'))

candidates=[]
# appsettings
for path in [
    '/opt/qalaa-runtime/SalesGateway/appsettings.json',
    '/opt/qalaa-runtime/SalesGateway/appsettings.Production.json',
]:
    if os.path.isfile(path):
        try:
            data=json.loads(open(path,encoding='utf-8').read())
            cs=(data.get('ConnectionStrings') or {})
            for k,v in cs.items():
                candidates.append((path+':'+k, v))
        except Exception as ex:
            print('JSON_ERR', path, ex)

# env files
for path in [
    '/etc/qalaa-production/sales-gateway.env',
    '/etc/qalaa-production/delegated-manager.env',
    '/etc/qalaa-production/delegate-najaf-sales-gateway.env',
]:
    if os.path.isfile(path):
        txt=open(path,encoding='utf-8',errors='replace').read()
        for line in txt.splitlines():
            if 'ConnectionString' in line or 'SalesGateway' in line or 'Data Source' in line or 'Server=' in line:
                candidates.append((path, line.strip()[:200]))
                m=re.search(r'=(.+)$', line)
                if m and ('Server=' in m.group(1) or 'Data Source=' in m.group(1)):
                    candidates.append((path+':val', m.group(1).strip().strip('"')))

# systemd units
units=subprocess.getoutput("systemctl list-units --type=service --all --no-legend | awk '{print $1}' | grep -iE 'sales|gateway|delegated'")
print('UNITS', units)
for u in units.splitlines():
    u=u.strip()
    if not u: continue
    env=subprocess.getoutput(f"systemctl show {u} -p Environment --value")
    for part in env.split():
        if 'ConnectionString' in part or 'SalesGateway' in part:
            candidates.append((u, part[:220]))
            if '=' in part:
                candidates.append((u+':cs', part.split('=',1)[1]))

print('CANDIDATES', len(candidates))
for src,val in candidates:
    print('C', src, (val[:120]+'...') if len(val)>120 else val)

# prefer :val extracts (full line), then others
cs=''
src=''
ordered=sorted(candidates, key=lambda x: (0 if x[0].endswith(':val') else 1))
for s,v in ordered:
    val=v
    if val.startswith('ConnectionStrings__') or 'ConnectionStrings__SalesGateway=' in val:
        val=val.split('=',1)[1].strip().strip('"')
    if ('Server=' in val or 'Data Source=' in val) and ('Database=' in val or 'Initial Catalog=' in val):
        cs=val; src=s; break

# Also try reading appsettings ConnectionStrings.SalesGateway specifically
if not cs and os.path.isfile('/opt/qalaa-runtime/SalesGateway/appsettings.json'):
    data=json.loads(open('/opt/qalaa-runtime/SalesGateway/appsettings.json',encoding='utf-8').read())
    cs=(data.get('ConnectionStrings') or {}).get('SalesGateway') or (data.get('ConnectionStrings') or {}).get('DefaultConnection') or ''
    src='appsettings.SalesGateway'
    print('APPSETTINGS_KEYS', list((data.get('ConnectionStrings') or {}).keys()))

print('CHOSEN_SRC', src, 'LEN', len(cs or ''))
if not cs:
    # dump appsettings (redact password partially)
    p='/opt/qalaa-runtime/SalesGateway/appsettings.json'
    if os.path.isfile(p):
        txt=open(p,encoding='utf-8').read()
        txt=re.sub(r'(Password|Pwd)=[^;"]+','Password=***',txt,flags=re.I)
        print('APPSETTINGS', txt[:2000])
    raise SystemExit('no cs')

server,db,user,pwd=parse_cs(cs)
print('TARGET', server, db, 'user', user)

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
USING (SELECT N'{APP_KEY}' AS AppKey) AS source ON target.AppKey = source.AppKey
WHEN MATCHED THEN UPDATE SET
    TitleAr = N'{TITLE}',
    VideoUrl = N'{VIDEO_URL}',
    IsEnabled = 1,
    UpdatedAtUtc = SYSUTCDATETIME(),
    UpdatedBy = N'ops-upload'
WHEN NOT MATCHED THEN INSERT
    (AppKey, TitleAr, VideoUrl, IsEnabled, UpdatedAtUtc, UpdatedBy)
    VALUES
    (N'{APP_KEY}', N'{TITLE}', N'{VIDEO_URL}', 1, SYSUTCDATETIME(), N'ops-upload');

SELECT AppKey, TitleAr, VideoUrl, IsEnabled FROM dbo.MobileAppTutorialLinks WHERE AppKey = N'{APP_KEY}';
"""
open('/tmp/set_follower_tutorial.sql','w',encoding='utf-8').write(sql)
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i','/tmp/set_follower_tutorial.sql'], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-2000:])
print('SQL_EXIT', r.returncode)

# find listening port for SalesGateway
print(subprocess.getoutput("ss -lntp | grep -i BE_SalesEmployee | head -10"))
print(subprocess.getoutput("systemctl list-units --type=service --all --no-legend | grep -i sales | head -20"))
for url in [
    f'http://127.0.0.1:5280/api/mobile-tutorial-links/{APP_KEY}',
    f'http://127.0.0.1:5080/api/mobile-tutorial-links/{APP_KEY}',
    f'http://127.0.0.1:5000/api/mobile-tutorial-links/{APP_KEY}',
]:
    try:
        with urllib.request.urlopen(url, timeout=8) as resp:
            print('API_OK', url, resp.read().decode('utf-8','replace')[:300])
            break
    except Exception as ex:
        print('API_FAIL', url, ex)
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
        with sftp.file("/tmp/set_follower_tutorial_v2.py", "w") as f:
            f.write(REMOTE)
    _, out, err = c.exec_command("python3 /tmp/set_follower_tutorial_v2.py", timeout=180)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_follower_tutorial_v2_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1000:])
    c.close()
    return 0 if ("KtrCPWMogG4" in text and "SQL_EXIT 0" in text) else 1


if __name__ == "__main__":
    raise SystemExit(main())
