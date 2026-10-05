#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import ssl
import urllib.error
import urllib.request

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]


def probe_local():
    print("=== from local ===")
    for u in [
        "http://sharenewnajaf.alsaaeidy.com/api/Users/Users_LoginAdmin",
        "https://sharenewnajaf.alsaaeidy.com/api/Users/Users_LoginAdmin",
    ]:
        try:
            req = urllib.request.Request(
                u,
                data=json.dumps({"userName": "ahmed", "password": "x"}).encode(),
                headers={"Content-Type": "application/json"},
                method="POST",
            )
            with urllib.request.urlopen(req, timeout=20, context=ssl.create_default_context()) as r:
                print(u, r.status, r.read()[:200])
        except urllib.error.HTTPError as e:
            print(u, "HTTP", e.code, e.read()[:200])
        except Exception as e:
            print(u, "ERR", type(e).__name__, e)


def main() -> int:
    probe_local()
    remote = r'''
import json, urllib.request, urllib.error, subprocess, os, glob, time
u='qalaa-company-sharenewnajaf'
print(subprocess.check_output(['systemctl','show',u,'-p','FragmentPath','-p','WorkingDirectory','-p','ExecStart','-p','ActiveState','-p','MainPID','--no-pager'], text=True))
print('--- journal ---')
print(subprocess.check_output(['journalctl','-u',u,'-n','50','--no-pager'], text=True)[-3500:])
print('--- ss ---')
print(subprocess.check_output('ss -lntp | grep -E "dotnet|Kestrel" || true', shell=True, text=True)[:2000])
wd=subprocess.check_output(['systemctl','show',u,'-p','WorkingDirectory','--value'], text=True).strip()
print('WD', wd)
dll=os.path.join(wd,'BE_Company.dll') if wd else ''
print('DLL', dll, 'exists', os.path.isfile(dll) if dll else False)
if dll and os.path.isfile(dll):
    print('DLL_MTIME', time.ctime(os.path.getmtime(dll)))

# find port from EnvironmentFile / unit
env=subprocess.check_output(['systemctl','show',u,'-p','Environment','--value'], text=True)
print('ENV_SNIP', env[:500])
ef=subprocess.check_output(['systemctl','show',u,'-p','EnvironmentFiles','--value'], text=True)
print('ENVFILES', ef)

for url in [
 'http://sharenewnajaf.alsaaeidy.com/api/Users/Users_LoginAdmin',
]:
  try:
    req=urllib.request.Request(url, data=json.dumps({'userName':'ahmed','password':'x'}).encode(), headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req, timeout=20) as r:
      print('OK', url, r.status, r.read()[:200])
  except urllib.error.HTTPError as e:
    print('HTTP', url, e.code, e.read()[:200])
  except Exception as e:
    print('ERR', url, e)

for p in glob.glob('/etc/nginx/sites-enabled/*') + glob.glob('/etc/nginx/conf.d/*'):
  try:
    txt=open(p,encoding='utf-8',errors='replace').read()
  except Exception:
    continue
  if 'sharenewnajaf' in txt or 'Najaf' in txt:
    print('NGINX_FILE', p)
    print(txt[:1500])
    print('---')
'''
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/diag_najaf_login.py", "w") as f:
                f.write(remote)
        i, o, e = c.exec_command("python3 /tmp/diag_najaf_login.py", timeout=90)
        print("=== from server ===")
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
