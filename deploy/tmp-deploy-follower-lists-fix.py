#!/usr/bin/env python3
"""Deploy BE_Company follower-lists fix to all Admin* branches; smoke Kirkuk count."""
from __future__ import annotations

import json
import os
import tarfile
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")
BE_OUT = ROOT / "deploy" / "tmp-be-follower-lists"
BE_TAR = ROOT / "deploy" / "tmp-be-follower-lists.tgz"


def main() -> int:
    if not SSH_PASS or not BE_OUT.is_dir():
        print("missing SSH_PASS or publish dir")
        return 2

    with tarfile.open(BE_TAR, "w:gz") as t:
        for p in BE_OUT.rglob("*"):
            if not p.is_file():
                continue
            name = p.name.lower()
            if name.startswith("appsettings") and name.endswith(".json"):
                continue
            t.add(p, arcname=p.relative_to(BE_OUT).as_posix())
    print("be_tar", BE_TAR.stat().st_size)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=40,
        allow_agent=False,
        look_for_keys=False,
    )
    sftp = c.open_sftp()
    sftp.put(str(BE_TAR), "/tmp/be-follower-lists.tgz")
    sftp.close()

    remote = r"""
set -e
TS=$(date +%Y%m%d_%H%M%S)
for d in /opt/qalaa-runtime/Admin*; do
  [ -d "$d" ] || continue
  base=$(basename "$d")
  case "$base" in
    AdminFrontend*|Admin*backup*|Admin*-old*|AdminStop|AdminTest|AdminMonthMenu|*.bak-*) continue ;;
  esac
  [ -f "$d/BE_Company.dll" ] || continue
  unit=""
  for u in $(systemctl list-units --type=service --all --no-legend | awk '{print $1}'); do
    wd=$(systemctl show -p WorkingDirectory --value "$u" 2>/dev/null || true)
    if [ "$wd" = "$d" ]; then unit="$u"; break; fi
  done
  echo "DEPLOY $base -> ${unit:-NONE}"
  [ -n "$unit" ] && systemctl stop "$unit" || true
  cp -a "$d" "${d}.bak-followerlists-$TS"
  tar -xzf /tmp/be-follower-lists.tgz -C "$d"
  chown -R qalaa:qalaa "$d" || true
  [ -n "$unit" ] && systemctl start "$unit" || true
done

# Smoke: Kirkuk service — count available without auth should be 401; with grep confirm no DevicePayment filter in dll
python3 <<'PY'
import os, re, subprocess, json, urllib.request, urllib.error
dll='/opt/qalaa-runtime/AdminKirkuk/BE_Company.dll'
# ensure DevicePayment filter string gone from available query region - soft check
raw=open(dll,'rb').read()
# old filter ascii
old=b'ISNULL(d.DevicePaymentState, 0) = 1'
print('OLD_FILTER_IN_DLL', old in raw)
# unit + port
unit='qalaa-company-sharenewrkarkok.service'
# discover
import glob
for u_line in subprocess.check_output(['systemctl','list-units','--type=service','--all','--no-legend'], text=True).splitlines():
    u=u_line.split()[0]
    wd=subprocess.check_output(['systemctl','show','-p','WorkingDirectory','--value',u], text=True).strip()
    if wd=='/opt/qalaa-runtime/AdminKirkuk':
        unit=u
        break
print('KIRKUK_UNIT', unit, subprocess.check_output(['systemctl','is-active',unit], text=True).strip())
env=subprocess.check_output(['systemctl','show','-p','Environment','--value',unit], text=True)
m=re.search(r'http://[^:]+:(\d+)', env)
port=m.group(1) if m else '?'
print('PORT', port)
# DB count
cfg=json.load(open('/opt/qalaa-runtime/AdminKirkuk/appsettings.json',encoding='utf-8'))
cs=list(cfg['ConnectionStrings'].values())[0]
def parse(cs):
  d={}
  for p in cs.split(';'):
    if '=' in p:
      k,v=p.split('=',1); d[k.strip().lower()]=v.strip()
  return d.get('data source') or d.get('server'), d.get('user id') or d.get('uid'), d.get('password') or d.get('pwd'), re.search(r'Initial Catalog=([^;]+)', cs, re.I).group(1)
server,user,pwd,db=parse(cs)
os.environ['SQLCMDPASSWORD']=pwd
out=subprocess.check_output(['/opt/mssql-tools18/bin/sqlcmd','-S',server,'-U',user,'-d',db,'-C','-h','-1','-W','-Q',
  "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Delegates; SELECT COUNT(*) FROM dbo.Delegates WHERE DelegateName LIKE N'%قادس%';"], text=True)
nums=[x.strip() for x in out.splitlines() if x.strip().lstrip('-').isdigit()]
print('DB_TOTAL', nums[0] if nums else '?', 'QADISIYAH', nums[1] if len(nums)>1 else '?')
# HTTP 401 proves route up
import urllib.request
try:
  urllib.request.urlopen(urllib.request.Request(f'http://127.0.0.1:{port}/api/follower-lists/available', method='GET'), timeout=15)
except urllib.error.HTTPError as e:
  print('HTTP', e.code)
PY
"""
    stdin, stdout, stderr = c.exec_command(remote, timeout=900)
    print(stdout.read().decode("utf-8", "replace"))
    err = stderr.read().decode("utf-8", "replace")
    if err.strip():
        print("STDERR", err[-3000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
