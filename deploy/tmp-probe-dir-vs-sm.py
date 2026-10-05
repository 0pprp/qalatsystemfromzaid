#!/usr/bin/env python3
"""Compare director vs SM الكل totals on gateway."""
import json
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, urllib.request

port = 5280

def login(path, user, pw):
    body = json.dumps({"userName": user, "password": pw}).encode()
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/{path}",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=20) as r:
        return json.loads(r.read().decode())

def fetch(token, qs):
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/sales-manager/sales-requests?{qs}",
        headers={"Authorization": "Bearer " + token},
    )
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read().decode())

dir_login = login("Auth/LoginDirector", "حيدر", "حيدر")
print("DIR", dir_login.get("userType"), bool(dir_login.get("token")))
d = fetch(dir_login["token"], "page=1&pageSize=5&tab=all")
print("DIR_TOTAL", d.get("total"), "tabs", d.get("tabCounts"))

# try common SM credentials from bootstrap
sm_ok = False
for user, pw in [("مدير", "مدير"), ("مديرمبيعات", "مديرمبيعات"), ("sales", "sales")]:
    try:
        # discover SM login endpoint via LinkCity is hard; use gateway Auth if exists
        for ep in ["Auth/LoginSalesManager", "Auth/Login", "Auth/LoginAdmin"]:
            try:
                sm = login(ep, user, pw)
                if sm.get("token") and ("مبيعات" in str(sm.get("userType") or "") or sm.get("salesManagerScope") or sm.get("SalesManagerScope")):
                    s = fetch(sm["token"], "page=1&pageSize=5&tab=all")
                    print("SM", ep, user, sm.get("userType"), "TOTAL", s.get("total"), "tabs", s.get("tabCounts"))
                    sm_ok = True
                    break
            except Exception as e:
                pass
        if sm_ok:
            break
    except Exception:
        pass
if not sm_ok:
    print("SM_SKIP no credentials")
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_dir_vs_sm.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_dir_vs_sm.py", timeout=180)
    print(out.read().decode("utf-8", "replace").encode("ascii", "backslashreplace").decode("ascii"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e.encode("ascii", "replace").decode("ascii")[:800])
    raise SystemExit(out.channel.recv_exit_status())
finally:
    c.close()
