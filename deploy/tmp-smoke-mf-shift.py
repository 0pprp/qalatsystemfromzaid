#!/usr/bin/env python3
"""Smoke MF shift start/location + DM live/trail on demo gateway :5403."""
from __future__ import annotations

import json
import os
import urllib.error
import urllib.request

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = r"""
import json, os, urllib.request, urllib.error

base='http://127.0.0.1:5403/api'
env=open('/etc/qalaa-demo/sales-gateway.env',encoding='utf-8',errors='replace').read()
mf_pw=None
for line in env.splitlines():
  if line.startswith('MainFollowerAccounts__0__Password='):
    mf_pw=line.split('=',1)[1]
    break
if not mf_pw:
  mf_pw=os.environ.get('MF_PASSWORD','')

def post(path, body, token=None):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, data=json.dumps(body).encode(), headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw=r.read().decode()
            return r.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        body=e.read().decode('utf-8','replace')
        try: data=json.loads(body)
        except Exception: data=body
        return e.code, data

def get(path, token=None):
    headers={}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw=r.read().decode()
            return r.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        body=e.read().decode('utf-8','replace')
        try: data=json.loads(body)
        except Exception: data=body
        return e.code, data

st, mf = post('/Auth/LoginMainFollower', {'userName':'mf1','password':mf_pw})
print('MF_LOGIN', st, bool(mf and mf.get('token')))
if st!=200 or not mf or not mf.get('token'):
    raise SystemExit(1)
mf_tok=mf['token']

st, cur = get('/main-follower/shift/current', mf_tok)
print('CURRENT_BEFORE', st, cur)

# end if already open
if isinstance(cur, dict) and cur.get('status')=='Open':
    st, ended = post('/main-follower/shift/end', {'latitude':32.0,'longitude':44.3}, mf_tok)
    print('END_OLD', st, ended and ended.get('status'))

st, started = post('/main-follower/shift/start', {'latitude':32.0251,'longitude':44.3352,'accuracyMeters':12}, mf_tok)
print('START', st, started and started.get('status'), started and started.get('shiftId'))
if st!=200:
    raise SystemExit(1)

st, loc = post('/main-follower/shift/location', {'latitude':32.0255,'longitude':44.3358,'accuracyMeters':10}, mf_tok)
print('LOC', st, loc and loc.get('id'), loc and loc.get('latitude'))

st, dm = post('/Auth/LoginDelegatedManager', {'userName':'dm','password':'dm'})
print('DM_LOGIN', st, bool(dm and dm.get('token')))
if st!=200 or not dm or not dm.get('token'):
    raise SystemExit(1)
dm_tok=dm['token']

st, live = get('/delegated-manager/committees/main-followers/live', dm_tok)
print('LIVE', st, len(live) if isinstance(live, list) else live)
if isinstance(live, list):
    mf1=[x for x in live if (x.get('userName') or '').lower()=='mf1']
    print('LIVE_MF1', mf1[0] if mf1 else None)

st, trail = get('/delegated-manager/committees/main-followers/mf1/trail', dm_tok)
print('TRAIL', st, len(trail) if isinstance(trail, list) else trail)

st, ended = post('/main-follower/shift/end', {'latitude':32.026,'longitude':44.336}, mf_tok)
print('END', st, ended and ended.get('status'))

st, cur2 = get('/main-follower/shift/current', mf_tok)
print('CURRENT_AFTER', st, cur2)
print('SMOKE_OK')
"""
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/smoke_mf_shift.py", "w") as f:
                f.write(remote)
        _, o, e = c.exec_command("python3 /tmp/smoke_mf_shift.py", timeout=120)
        out = o.read().decode("utf-8", "replace")
        err = e.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[-1000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
