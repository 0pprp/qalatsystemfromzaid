#!/usr/bin/env python3
"""Seed main-follower MobileAppReleases on production gateway SQL."""
from __future__ import annotations

import json
import os
import re
import subprocess

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, os, re, subprocess, urllib.request

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
SELECT AppKey, LatestVersionCode, ForceUpdate FROM dbo.MobileAppReleases WHERE AppKey = N'main-follower';
"""
open('/tmp/seed_mf_prod.sql', 'w', encoding='utf-8').write(sql)
cmd = [
    '/opt/mssql-tools18/bin/sqlcmd', '-S', server, '-d', db, '-C',
    '-i', '/tmp/seed_mf_prod.sql', '-U', user, '-P', pwd,
]
print(subprocess.check_output(cmd, text=True, stderr=subprocess.STDOUT)[-600:])
with urllib.request.urlopen(
    'http://127.0.0.1:5280/api/mobile-updates/main-follower?currentVersionCode=1',
    timeout=15,
) as r:
    print('PROD_OK', r.read()[:250].decode('utf-8', 'replace'))
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
with c.open_sftp() as sftp:
    with sftp.file("/tmp/seed_mf_prod.py", "w") as f:
        f.write(REMOTE)
i, o, e = c.exec_command("python3 /tmp/seed_mf_prod.py", timeout=90)
print(o.read().decode("utf-8", "replace"))
err = e.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[-800:])
print("exit", o.channel.recv_exit_status())
c.close()
