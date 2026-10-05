#!/usr/bin/env python3
import json
import os
import re

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, re, urllib.request
from pathlib import Path
import subprocess

wd = subprocess.getoutput("systemctl show qalaa-sales-gateway -p WorkingDirectory --value").strip()
print("wd", wd)
cfg = json.loads(Path(wd + "/appsettings.json").read_text(encoding="utf-8", errors="replace"))
acc = cfg.get("DirectorAccount") or {}
print("user", acc.get("UserName"), "hasPass", bool(acc.get("Password")))
pid = subprocess.getoutput("systemctl show qalaa-sales-gateway -p MainPID --value").strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
urls = [x.decode(errors="replace") for x in env if x.startswith(b"ASPNETCORE_URLS=")]
print("urls", urls)
port = 5100
for u in urls:
    m = re.search(r":(\d+)", u)
    if m:
        port = int(m.group(1))
print("port", port)

body = json.dumps({"userName": acc.get("UserName") or "", "password": acc.get("Password") or ""}).encode()
req = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/Auth/LoginDirector",
    data=body,
    headers={"Content-Type": "application/json"},
    method="POST",
)
with urllib.request.urlopen(req, timeout=20) as r:
    login = json.loads(r.read().decode())
print("login", login.get("userType"), bool(login.get("token")))
token = login["token"]

# Policy marker in dll
dll = Path(wd + "/BE_SalesEmployee.dll").read_bytes()
print("hasOrDirector", b"SalesManagerOrDirector" in dll)
print("hasDenyWrites", b"DenyDirectorWrites" in dll or "DenyDirectorWrites".encode("utf-16le") in dll)

req2 = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/sales-manager/sales-requests?page=1&pageSize=5&tab=all",
    headers={"Authorization": "Bearer " + token},
)
try:
    with urllib.request.urlopen(req2, timeout=120) as r2:
        data = json.loads(r2.read().decode())
    print(
        "OK total",
        data.get("total"),
        "items",
        len(data.get("items") or []),
        "tabCountsSample",
        list((data.get("tabCounts") or {}).items())[:8],
    )
except Exception as e:
    print("ERR", getattr(e, "code", None), e)
    if hasattr(e, "read"):
        print(e.read()[:1000].decode("utf-8", "replace"))

# Compare: sales manager login if configured
for key in ("SalesManagerAccount", "CentralSalesManagerAccount", "CentralAccount"):
    sm = cfg.get(key)
    if isinstance(sm, dict) and sm.get("UserName"):
        print("try SM key", key, sm.get("UserName"))
'''


def main() -> int:
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_dir_requests.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, out, err = c.exec_command("python3 /tmp/probe_dir_requests.py", timeout=180)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[:1200])
        return out.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
