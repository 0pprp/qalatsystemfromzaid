#!/usr/bin/env python3
import json
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, urllib.request

port = 5280
# bootstrap director
body = json.dumps({"userName": "حيدر", "password": "حيدر"}).encode()
req = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/Auth/LoginDirector",
    data=body,
    headers={"Content-Type": "application/json"},
    method="POST",
)
with urllib.request.urlopen(req, timeout=20) as r:
    login = json.loads(r.read().decode())
print("login", login.get("userType"), bool(login.get("token")), login.get("userName"))
token = login["token"]

for label, path in [
    ("all", "sales-manager/sales-requests?page=1&pageSize=5&tab=all"),
    ("branches", "sales-manager/branches"),
    ("unread", "sales-manager/sales-requests/unread-count"),
]:
    req2 = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/{path}",
        headers={"Authorization": "Bearer " + token},
    )
    try:
        with urllib.request.urlopen(req2, timeout=120) as r2:
            data = json.loads(r2.read().decode())
        if label == "all":
            print("DIR_OK total", data.get("total"), "items", len(data.get("items") or []), "tabs", data.get("tabCounts"))
        elif label == "branches":
            print("BRANCHES", len(data) if isinstance(data, list) else data)
        else:
            print("UNREAD", data)
    except Exception as e:
        print("FAIL", label, getattr(e, "code", None))
        if hasattr(e, "read"):
            print(e.read()[:500].decode("utf-8", "replace"))
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_dir4.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_dir4.py", timeout=180)
    print(out.read().decode("utf-8", "replace").encode("ascii", "backslashreplace").decode("ascii"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e.encode("ascii", "replace").decode("ascii")[:800])
    raise SystemExit(out.channel.recv_exit_status())
finally:
    c.close()
