#!/usr/bin/env python3
import os, json, urllib.request
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('169.58.236.52', username='root', password=os.environ['SSH_PASS'], timeout=60, allow_agent=False, look_for_keys=False)
remote = r'''
import json, urllib.request
for app in ('delegate','delegated-manager','follower','main-follower'):
    try:
        with urllib.request.urlopen(f'http://127.0.0.1:5280/api/mobile-updates/{app}?currentVersionCode=1', timeout=20) as r:
            d=json.load(r)
        keys=('latestVersionName','latestVersionCode','forceUpdate','updateAvailable','mandatory','minimumSupportedVersionCode','apkUrl')
        print(app, {k:d.get(k) for k in keys})
    except Exception as e:
        print(app, 'ERR', e)
'''
sftp=c.open_sftp()
with sftp.file('/tmp/check_live_versions.py','w') as f: f.write(remote)
sftp.close()
_,o,e=c.exec_command('python3 /tmp/check_live_versions.py', timeout=60)
print(o.read().decode())
print(e.read().decode())
c.close()
