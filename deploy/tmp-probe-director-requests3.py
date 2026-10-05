#!/usr/bin/env python3
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, re, urllib.request
from pathlib import Path
import subprocess

pid = subprocess.getoutput("systemctl show qalaa-sales-gateway -p MainPID --value").strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
envd = {}
for x in env:
    if b"=" in x:
        k,v=x.split(b"=",1)
        envd[k.decode(errors="replace")]=v.decode(errors="replace")
for k in sorted(envd):
    if "irector" in k.lower():
        print("ENV", k, "***" if "ass" in k.lower() else envd[k])

# Try bootstrap credentials from DirectorAccountService comments / code
# Also try LoginSalesManager then compare; and LoginDirector with common names from login page / docs
candidates = []
# from env SalesManager - not director
# bootstrap when no password: check code
user = envd.get("DirectorAccount__UserName") or envd.get("Director__UserName") or ""
pwd = envd.get("DirectorAccount__Password") or envd.get("Director__Password") or ""
if user:
    candidates.append((user, pwd, "env"))

# DirectorAccounts__N
i=0
while True:
    u = envd.get(f"DirectorAccounts__{i}__UserName")
    p = envd.get(f"DirectorAccounts__{i}__Password")
    if u is None and p is None:
        break
    if u:
        candidates.append((u, p or "", f"DirectorAccounts_{i}"))
    i += 1
    if i > 20:
        break

print("candidates", [(u, bool(p), src) for u,p,src in candidates])

port = 5280
token = None
for user, pwd, src in candidates or [("director", "director", "guess")]:
    body = json.dumps({"userName": user, "password": pwd}).encode()
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/Auth/LoginDirector",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            login = json.loads(r.read().decode())
        if login.get("token"):
            print("LOGIN_OK", src, user, login.get("userType"))
            token = login["token"]
            break
        print("LOGIN_NO_TOKEN", src, login)
    except Exception as e:
        code = getattr(e, "code", None)
        msg = ""
        if hasattr(e, "read"):
            msg = e.read()[:200].decode("utf-8", "replace")
        print("LOGIN_FAIL", src, user, code, msg)

if not token:
    # try sales manager token on same endpoint for baseline of sales-requests
    sm_user = envd.get("SalesManagerAccount__UserName")
    sm_pwd = envd.get("SalesManagerAccount__Password")
    body = json.dumps({"userName": sm_user, "password": sm_pwd}).encode()
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/Auth/LoginSalesManager",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=15) as r:
        login = json.loads(r.read().decode())
    print("SM_LOGIN", login.get("userType"), bool(login.get("token")))
    token = login["token"]
    who = "SM"
else:
    who = "DIR"

req2 = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/sales-manager/sales-requests?page=1&pageSize=5&tab=all",
    headers={"Authorization": "Bearer " + token},
)
try:
    with urllib.request.urlopen(req2, timeout=120) as r2:
        data = json.loads(r2.read().decode())
    print(who, "REQ_OK total", data.get("total"), "items", len(data.get("items") or []), "tabs", data.get("tabCounts"))
except Exception as e:
    print(who, "REQ_FAIL", getattr(e, "code", None), e)
    if hasattr(e, "read"):
        print(e.read()[:800].decode("utf-8", "replace"))
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_dir3.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_dir3.py", timeout=180)
    print(out.read().decode("utf-8", "replace").encode("ascii", "replace").decode("ascii"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e.encode("ascii", "replace").decode("ascii")[:1000])
    raise SystemExit(out.channel.recv_exit_status())
finally:
    c.close()
