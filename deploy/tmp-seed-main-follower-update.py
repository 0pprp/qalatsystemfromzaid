#!/usr/bin/env python3
"""Ensure MobileAppReleases has main-follower row on gateway SQL (demo+prod if possible)."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
python3 - <<'PY'
import os, json, urllib.request, urllib.error, subprocess, re

# Find connection string from gateway env/appsettings
def find_cs():
    for path in [
        '/opt/qalaa-demo/sales-gateway/appsettings.json',
        '/opt/qalaa-runtime/SalesGateway/appsettings.json',
        '/etc/qalaa-demo/sales-gateway.env',
        '/etc/qalaa-production/sales-gateway.env',
    ]:
        if not os.path.isfile(path):
            continue
        txt=open(path,encoding='utf-8',errors='replace').read()
        if path.endswith('.json'):
            try:
                data=json.loads(txt)
                cs=(data.get('ConnectionStrings') or {}).get('SalesGateway') or ''
                if cs: return cs, path
            except Exception:
                pass
        m=re.search(r'ConnectionStrings__SalesGateway=(.+)', txt)
        if m: return m.group(1).strip().strip('"'), path
        m=re.search(r'SalesGateway=(.+)', txt)
        if m and 'Server=' in m.group(1): return m.group(1).strip().strip('"'), path
    # systemd environment
    env=subprocess.check_output(['systemctl','show','qalaa-demo-sales-gateway','-p','Environment','--value'], text=True)
    m=re.search(r'ConnectionStrings__SalesGateway=([^\s]+)', env)
    if m: return m.group(1), 'systemd-demo'
    env=subprocess.check_output(['systemctl','show','qalaa-sales-gateway','-p','Environment','--value'], text=True)
    m=re.search(r'ConnectionStrings__SalesGateway=([^\s]+)', env)
    if m: return m.group(1), 'systemd-prod'
    return '', ''

cs, src = find_cs()
print('CS_SRC', src)
print('CS_HAS', bool(cs), 'LEN', len(cs))

sql = """
IF NOT EXISTS (SELECT 1 FROM dbo.MobileAppReleases WHERE AppKey = N'main-follower')
BEGIN
    INSERT INTO dbo.MobileAppReleases
        (AppKey, LatestVersionName, LatestVersionCode, MinimumSupportedVersionCode, ForceUpdate, ApkUrl, Sha256, ReleaseNotes)
    VALUES
        (N'main-follower', N'1.0.0', 1, 1, 0,
         N'https://example.invalid/main-follower.apk',
         N'0000000000000000000000000000000000000000000000000000000000000000',
         N'إصدار أولي للمتابع الرئيسي');
END
ELSE
BEGIN
    UPDATE dbo.MobileAppReleases
      SET LatestVersionName = N'1.0.0',
          LatestVersionCode = 1,
          MinimumSupportedVersionCode = 1,
          ForceUpdate = 0,
          ReleaseNotes = N'إصدار أولي للمتابع الرئيسي',
          UpdatedAtUtc = SYSUTCDATETIME()
      WHERE AppKey = N'main-follower';
END
SELECT AppKey, LatestVersionCode, ForceUpdate FROM dbo.MobileAppReleases WHERE AppKey=N'main-follower';
"""

# Try sqlcmd with parsed bits if possible; else use docker/sql tools
# Prefer /opt tools
def run_sqlcmd(cs):
    # parse Server=...;Database=...;User Id=...;Password=...
    parts={}
    for p in cs.split(';'):
        if '=' in p:
            k,v=p.split('=',1)
            parts[k.strip().lower()]=v.strip()
    server=parts.get('server') or parts.get('data source')
    db=parts.get('database') or parts.get('initial catalog')
    user=parts.get('user id') or parts.get('uid')
    pwd=parts.get('password') or parts.get('pwd')
    print('SQL', server, db, bool(user))
    if not server or not db:
        raise SystemExit('bad cs')
    # write sql file
    open('/tmp/seed_mf_update.sql','w',encoding='utf-8').write(sql)
    cmd=['/opt/mssql-tools18/bin/sqlcmd','-S',server,'-d',db,'-C','-i','/tmp/seed_mf_update.sql']
    if user:
        cmd += ['-U',user,'-P',pwd]
    else:
        cmd += ['-E']
    # fallbacks
    for binpath in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
        cmd[0]=binpath
        try:
            out=subprocess.check_output(cmd, stderr=subprocess.STDOUT, text=True)
            print(out[-800:])
            return
        except FileNotFoundError:
            continue
        except subprocess.CalledProcessError as e:
            print('SQLCMD_FAIL', e.output[-800:] if e.output else e)
            raise
    raise SystemExit('no sqlcmd')

if cs:
    run_sqlcmd(cs)
else:
    print('NO_CS — demo may be in-memory; checking API after restart note')

# verify API
for base in ['http://127.0.0.1:5403/api','http://127.0.0.1:5280/api']:
    try:
        with urllib.request.urlopen(base+'/mobile-updates/main-follower?currentVersionCode=1', timeout=15) as r:
            body=json.loads(r.read().decode())
        print('OK', base, body.get('latestVersionCode'), body.get('updateKind'))
    except Exception as e:
        print('FAIL', base, e)
PY
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
i, o, e = c.exec_command(REMOTE, timeout=120)
print(o.read().decode("utf-8", "replace"))
err = e.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[-1000:])
print("exit", o.channel.recv_exit_status())
c.close()
