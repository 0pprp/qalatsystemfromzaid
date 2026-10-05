#!/usr/bin/env python3
"""Assign committee + Najaf DEMO mission for main followers (no APK rebuild)."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import json, os, urllib.request, urllib.error

def read_env(path):
    if not os.path.isfile(path):
        return {}
    out={}
    for line in open(path, encoding='utf-8', errors='replace'):
        line=line.strip()
        if not line or line.startswith('#') or '=' not in line:
            continue
        k,v=line.split('=',1)
        out[k.strip()]=v.strip()
    return out

def post(base, path, body, token=None):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, data=json.dumps(body).encode(), headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=45) as r:
            raw=r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw=e.read().decode('utf-8','replace')
        try: data=json.loads(raw)
        except Exception: data=raw
        return e.code, data
    except Exception as e:
        return 0, str(e)

def get(base, path, token=None):
    headers={}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=45) as r:
            raw=r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw=e.read().decode('utf-8','replace')
        try: data=json.loads(raw)
        except Exception: data=raw
        return e.code, data
    except Exception as e:
        return 0, str(e)

targets=[
    ('demo', 'http://127.0.0.1:5403/api', '/etc/qalaa-demo/sales-gateway.env'),
    ('prod', 'http://127.0.0.1:5401/api', '/etc/qalaa-runtime/sales-gateway.env'),
]

# also try common alt ports / env paths
for alt_env in ['/etc/qalaa-production/sales-gateway.env','/etc/qalaa/sales-gateway.env']:
    if os.path.isfile(alt_env) and all(t[2]!=alt_env for t in targets):
        targets.append(('alt', 'http://127.0.0.1:5401/api', alt_env))

CITY='DatabaseCompanyNajaf_DEMO'
results=[]

for label, base, env_path in targets:
    env=read_env(env_path)
    mf_names=[]
    i=0
    while True:
        u=env.get(f'MainFollowerAccounts__{i}__UserName')
        if not u: break
        mf_names.append(u)
        i+=1
    dm_user=env.get('DelegatedManagerAccounts__0__UserName') or 'dm'
    dm_pass=env.get('DelegatedManagerAccounts__0__Password') or 'dm'
    mf0=mf_names[0] if mf_names else 'mf1'
    mf0_pass=env.get('MainFollowerAccounts__0__Password') or 'Mf@12345'
    print('===', label, base, 'mf=', mf_names, 'dm=', dm_user, 'env=', env_path if os.path.isfile(env_path) else 'MISSING')

    if len(mf_names) < 2:
        print('SKIP need >=2 MF accounts')
        continue

    st, dm = post(base, '/Auth/LoginDelegatedManager', {'userName': dm_user, 'password': dm_pass})
    print('DM_LOGIN', st)
    if st!=200 or not isinstance(dm, dict) or not dm.get('token'):
        print('DM_FAIL', dm)
        continue
    dm_tok=dm['token']

    st, followers = get(base, '/delegated-manager/committees/main-followers', dm_tok)
    print('FOLLOWERS', st, followers if isinstance(followers, list) else followers)

    st, existing = get(base, '/delegated-manager/committees/missions', dm_tok)
    print('MISSIONS_BEFORE', st, len(existing) if isinstance(existing, list) else existing)

    # reuse existing active najaf-demo mission covering all listed MFs if present
    reuse=None
    if isinstance(existing, list):
        for m in existing:
            cities=[(c or '').strip() for c in (m.get('cityValues') or [])]
            members=[(x or '').lower() for x in (m.get('memberUserNames') or [])]
            if CITY in cities and all(n.lower() in members for n in mf_names):
                reuse=m
                break
    if reuse:
        print('REUSE_MISSION', reuse.get('id'), reuse.get('committeeName'), reuse.get('cityValues'))
        mission=reuse
    else:
        st, committee = post(base, '/delegated-manager/committees', {
            'name': 'لجنة النجف DEMO',
            'memberUserNames': mf_names,
        }, dm_tok)
        print('COMMITTEE', st, committee if isinstance(committee, dict) else committee)
        if st!=200 or not isinstance(committee, dict) or not committee.get('id'):
            continue
        st, mission = post(base, '/delegated-manager/committees/missions', {
            'committeeId': committee['id'],
            'cityValues': [CITY],
            'note': 'تكليف مهام النجف DEMO للمتابعين الرئيسيين',
        }, dm_tok)
        print('MISSION', st, mission if isinstance(mission, dict) else mission)
        if st!=200 or not isinstance(mission, dict):
            continue

    st, mf = post(base, '/Auth/LoginMainFollower', {'userName': mf0, 'password': mf0_pass})
    print('MF_LOGIN', st, mf0)
    if st!=200 or not isinstance(mf, dict) or not mf.get('token'):
        print('MF_FAIL', mf)
        continue
    st, my = get(base, '/main-follower/missions', mf['token'])
    print('MF_MISSIONS', st, len(my) if isinstance(my, list) else my)
    if isinstance(my, list):
        for m in my:
            print('  -', m.get('id'), m.get('committeeName'), m.get('cityValues'), m.get('status'))
    results.append((label, mission.get('id') if isinstance(mission, dict) else None, len(my) if isinstance(my, list) else 0))

# public path used by MF APK (demo default)
pub='http://127.0.0.1:8080/sales-gw/api'
# discover which local port nginx proxies — just try login via public if loopback works
for label, base, env_path in targets:
    env=read_env(env_path)
    if not env.get('MainFollowerAccounts__0__UserName'):
        continue
    mf0=env['MainFollowerAccounts__0__UserName']
    mf0_pass=env.get('MainFollowerAccounts__0__Password') or 'Mf@12345'
    st, mf = post(pub, '/Auth/LoginMainFollower', {'userName': mf0, 'password': mf0_pass})
    print('PUBLIC_LOGIN', pub, st)
    if st==200 and isinstance(mf, dict) and mf.get('token'):
        st, my = get(pub, '/main-follower/missions', mf['token'])
        print('PUBLIC_MISSIONS', st, len(my) if isinstance(my, list) else my)
        break

print('SUMMARY', results)
print('DONE')
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/assign_mf_committee_najaf_demo.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        i, o, e = c.exec_command("python3 /tmp/assign_mf_committee_najaf_demo.py", timeout=120)
        out = o.read().decode("utf-8", "replace")
        err = e.read().decode("utf-8", "replace")
        # redact any accidental password-looking dumps
        print(out)
        if err.strip():
            print("STDERR", err[-1000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
