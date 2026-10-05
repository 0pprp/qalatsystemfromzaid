#!/usr/bin/env python3
import json
import os
import urllib.error
import urllib.request

import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ.get("SSH_PASS", ""),
    timeout=40,
    allow_agent=False,
    look_for_keys=False,
)

remote = r'''
python3 <<'PY'
import json, urllib.request, urllib.error, os, subprocess

dll='/opt/qalaa-runtime/AdminNajaf/BE_Company.dll'
print('dll_size', os.path.getsize(dll))
# check type names in dll via monodis/dotnet if available
r=subprocess.run(['bash','-lc', f'grep -a -o "LegalExcelSearch" {dll} | head -3'], capture_output=True, text=True)
print('grep_legal', repr(r.stdout), repr(r.stderr[:200] if r.stderr else ''))

def post(url, body):
    data=json.dumps(body).encode()
    req=urllib.request.Request(url, data=data, method='POST', headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            return resp.status, resp.read()[:300].decode('utf-8','replace')
    except urllib.error.HTTPError as e:
        return e.code, e.read()[:300].decode('utf-8','replace')

for url in [
 'http://127.0.0.1:5207/api/Customers/LegalExcelSearch',
 'http://127.0.0.1:5207/api/customers/LegalExcelSearch',
]:
    code, body = post(url, {'queries':[{'name':'علي','phone':'07800000000'}]})
    print('POST', url, code, body)

# compare known customers endpoint
req=urllib.request.Request('http://127.0.0.1:5207/api/Customers/Customers_InfoSimple/1')
try:
    with urllib.request.urlopen(req, timeout=20) as resp:
        print('INFO', resp.status)
except urllib.error.HTTPError as e:
    print('INFO', e.code, e.read()[:120].decode('utf-8','replace'))

# journal
print(subprocess.check_output(['journalctl','-u','qalaa-company-sharenewnajaf.service','-n','30','--no-pager'], text=True, errors='replace')[-2000:])
PY
'''
stdin, stdout, stderr = c.exec_command(remote, timeout=60)
print(stdout.read().decode("utf-8", "replace"))
print(stderr.read().decode("utf-8", "replace")[-1000:])
c.close()
