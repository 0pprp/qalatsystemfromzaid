#!/usr/bin/env python3
"""Read-only audit: follower available lists vs all Delegates per branch DB."""
from __future__ import annotations

import os

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
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
    remote = r"""
python3 <<'PY'
import json, os, re, glob, subprocess

cfg=json.load(open('/opt/qalaa-runtime/AdminNajaf/appsettings.json',encoding='utf-8'))
cs=list(cfg.get('ConnectionStrings',{}).values())[0]

def parse_cs(cs):
    d={}
    for part in cs.split(';'):
        if '=' in part:
            k,v=part.split('=',1)
            d[k.strip().lower()]=v.strip()
    server=d.get('data source') or d.get('server')
    user=d.get('user id') or d.get('uid')
    pwd=d.get('password') or d.get('pwd')
    return server,user,pwd

server,user,pwd=parse_cs(cs)
print('SQL_SERVER', server)
os.environ['SQLCMDPASSWORD']=pwd

dbs=[]
for p in sorted(glob.glob('/opt/qalaa-runtime/Admin*/appsettings.json')):
    base=os.path.basename(os.path.dirname(p))
    if 'Frontend' in base or 'backup' in base.lower() or base.endswith('-old') or base in ('AdminStop','AdminTest','AdminMonthMenu'):
        continue
    try:
        j=json.load(open(p,encoding='utf-8'))
        csl=list(j.get('ConnectionStrings',{}).values())
        if not csl: continue
        m=re.search(r'Initial Catalog=([^;]+)', csl[0], re.I)
        if not m: continue
        dbs.append((base, m.group(1)))
    except Exception as e:
        print('CFG_ERR', base, e)

print('BRANCHES', len(dbs))
gaps=[]
for base, db in dbs:
    q=("SET NOCOUNT ON; "
       "SELECT COUNT(*) FROM dbo.Delegates; "
       "SELECT COUNT(*) FROM dbo.Delegates WHERE ISNULL(DevicePaymentState,0)=1; "
       "SELECT COUNT(*) FROM dbo.Delegates WHERE ISNULL(DevicePaymentState,0)=0; "
       "SELECT COUNT(*) FROM dbo.Delegates WHERE DelegateName LIKE N'%قادس%';")
    cmd=['/opt/mssql-tools18/bin/sqlcmd','-S',server,'-U',user,'-d',db,'-C','-h','-1','-W','-Q',q]
    try:
        out=subprocess.check_output(cmd, text=True, stderr=subprocess.STDOUT, timeout=60)
    except subprocess.CalledProcessError as e:
        print('FAIL', base, db, (e.output or '')[:200])
        continue
    nums=[x.strip() for x in out.splitlines() if x.strip().lstrip('-').isdigit()]
    while len(nums)<4: nums.append('?')
    total, on, off, qad = nums[:4]
    try:
        missing=int(total)-int(on)
    except Exception:
        missing='?'
    flag='OK' if missing==0 else 'GAP'
    if flag=='GAP':
        gaps.append((base,db,total,on,off,qad,missing))
    print(f'{flag}\t{base}\t{db}\ttotal={total}\tpaymentOn={on}\tpaymentOff={off}\tqadisiyah={qad}\tUI_missing={missing}')

print('--- KIRKUK DETAIL ---')
for base, db in dbs:
    if 'Kirkuk' in base or 'karkok' in db.lower() or 'kirkuk' in db.lower():
        q2=("SET NOCOUNT ON; SELECT DelegateID, DelegateName, ISNULL(DevicePaymentState,0) AS DPS "
            "FROM dbo.Delegates ORDER BY DelegateName;")
        out2=subprocess.check_output(['/opt/mssql-tools18/bin/sqlcmd','-S',server,'-U',user,'-d',db,'-C','-W','-Q',q2], text=True, stderr=subprocess.STDOUT, timeout=60)
        print(base, db)
        print(out2)
print('GAP_COUNT', len(gaps))
PY
"""
    stdin, stdout, stderr = c.exec_command(remote, timeout=300)
    print(stdout.read().decode("utf-8", "replace"))
    err = stderr.read().decode("utf-8", "replace")
    if err.strip():
        print("STDERR", err[-2000:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
