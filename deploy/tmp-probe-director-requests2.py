#!/usr/bin/env python3
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, re, urllib.request, os
from pathlib import Path
import subprocess

wd = subprocess.getoutput("systemctl show qalaa-sales-gateway -p WorkingDirectory --value").strip()
print("wd", wd)
print("ls", sorted(os.listdir(wd))[:40])
cfg_path = None
for name in ("appsettings.json", "appsettings.Production.json", "appsettings.Production.Gateway.json"):
    p = Path(wd) / name
    if p.is_file():
        cfg_path = p
        break
# also search
if not cfg_path:
    for p in Path(wd).rglob("appsettings*.json"):
        cfg_path = p
        break
print("cfg", cfg_path)
cfg = json.loads(cfg_path.read_text(encoding="utf-8", errors="replace")) if cfg_path else {}
acc = cfg.get("DirectorAccount") or {}
# env overrides
pid = subprocess.getoutput("systemctl show qalaa-sales-gateway -p MainPID --value").strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
envd = {}
for x in env:
    if b"=" in x:
        k,v=x.split(b"=",1)
        envd[k.decode(errors="replace")]=v.decode(errors="replace")
print("ASPNETCORE_URLS", envd.get("ASPNETCORE_URLS"))
for k in sorted(envd):
    if "Director" in k or "DIRECTOR" in k or "SalesManager" in k:
        print("ENV", k, "***" if "Pass" in k or "PASS" in k or "Password" in k else envd[k])

user = acc.get("UserName") or envd.get("DirectorAccount__UserName") or ""
pwd = acc.get("Password") or envd.get("DirectorAccount__Password") or ""
print("loginAs", user, "hasPass", bool(pwd))

port = 5100
m = re.search(r":(\d+)", envd.get("ASPNETCORE_URLS") or "")
if m:
    port = int(m.group(1))
# also listen via ss
ss = subprocess.getoutput(f"ss -lntp | grep pid={pid}")
print("ss", ss[:300])
ports = [int(x) for x in re.findall(r"127\\.0\\.0\\.1:(\\d+)", ss)]
if ports:
    port = ports[0]
print("port", port)

dll = Path(wd) / "BE_SalesEmployee.dll"
if dll.is_file():
    b = dll.read_bytes()
    print("hasOrDirector", b"SalesManagerOrDirector" in b)
    print("hasDeny", b"DenyDirectorWrites" in b)

body = json.dumps({"userName": user, "password": pwd}).encode()
req = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/Auth/LoginDirector",
    data=body,
    headers={"Content-Type": "application/json"},
    method="POST",
)
try:
    with urllib.request.urlopen(req, timeout=20) as r:
        login = json.loads(r.read().decode())
except Exception as e:
    print("login fail", e)
    if hasattr(e, "read"):
        print(e.read()[:500])
    raise SystemExit(2)
print("login", login.get("userType"), bool(login.get("token")))
token = login["token"]

for path in [
    "sales-manager/sales-requests?page=1&pageSize=5&tab=all",
    "sales-manager/branches",
]:
    req2 = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/{path}",
        headers={"Authorization": "Bearer " + token},
    )
    try:
        with urllib.request.urlopen(req2, timeout=120) as r2:
            raw = r2.read().decode()
            data = json.loads(raw)
        if path.startswith("sales-manager/sales-requests"):
            print("REQ OK total", data.get("total"), "items", len(data.get("items") or []), "tabs", data.get("tabCounts"))
        else:
            print("BRANCHES", len(data) if isinstance(data, list) else type(data), str(data)[:200])
    except Exception as e:
        print("FAIL", path, getattr(e, "code", None), e)
        if hasattr(e, "read"):
            print(e.read()[:800].decode("utf-8", "replace"))
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_dir_requests2.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_dir_requests2.py", timeout=180)
    raw = out.read().decode("utf-8", "replace")
    print(raw.encode("ascii", "replace").decode("ascii"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e.encode("ascii", "replace").decode("ascii")[:1500])
    raise SystemExit(out.channel.recv_exit_status())
finally:
    c.close()
