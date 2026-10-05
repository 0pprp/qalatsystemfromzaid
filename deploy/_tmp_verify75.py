#!/usr/bin/env python3
from __future__ import annotations

import os
import sys
import time

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import base64, json, subprocess, time, urllib.request
from pathlib import Path

subprocess.run(['systemctl','restart','qalaa-company-sharenewnajaf'], check=False)
for i in range(20):
    time.sleep(2)
    st = subprocess.check_output(['systemctl','is-active','qalaa-company-sharenewnajaf'], text=True).strip()
    if st == 'active':
        break
print('status', st)
time.sleep(3)
pid = subprocess.check_output(['systemctl','show','qalaa-company-sharenewnajaf','-p','MainPID','--value'], text=True).strip()
env = Path(f'/proc/{pid}/environ').read_bytes().split(b'\0')
key = ''
for item in env:
    if item.startswith(b'SalesEmployee__GatewayKey='):
        key = item.split(b'=',1)[1].decode()
headers = {'X-Sales-Gateway-Key': key, 'X-Sales-Manager-Name-B64': base64.b64encode(b'SalesManager').decode()}
req = urllib.request.Request('http://127.0.0.1:5301/api/sales-manager/stats/branch-month', headers=headers)
with urllib.request.urlopen(req, timeout=120) as resp:
    raw = json.loads(resp.read().decode())
    cur = raw.get('current') or raw.get('Current') or {}
    print('NEW', cur.get('newCustomersCount') or cur.get('NewCustomersCount'))
    print('ZERO', cur.get('zeroedCustomersCount') or cur.get('ZeroedCustomersCount'))
    print('NET', cur.get('netCustomersCount') or cur.get('NetCustomersCount'))
"""

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file('/tmp/verify75.py', 'w') as f:
            f.write(REMOTE)
        sftp.close()
        _, o, e = c.exec_command('python3 /tmp/verify75.py', timeout=180)
        print(o.read().decode('utf-8', 'replace'))
        err = e.read().decode('utf-8', 'replace')
        if err.strip():
            print('STDERR', err[:1500])
        return o.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == '__main__':
    raise SystemExit(main())
