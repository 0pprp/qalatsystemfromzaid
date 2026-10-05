#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import base64, json, subprocess, urllib.request
from pathlib import Path

docs_dir = Path("/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs")
for i in [71,72,73,74,10]:
    matches = list(docs_dir.glob(f"{i}.*"))
    print("DISK", i, matches)

# DB FileKey for those ids via sqlcmd / docker? try sqlite no — use company API Read after auth
pid = subprocess.check_output(["systemctl", "show", "qalaa-company-sharenewnajaf", "-p", "MainPID", "--value"], text=True).strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
key = ""
for item in env:
    if item.startswith(b"SalesEmployee__GatewayKey="):
        key = item.split(b"=",1)[1].decode()
headers = {
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name-B64": base64.b64encode(b"SalesManager").decode(),
}

def get(url):
    req = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = resp.read(32)
            return resp.status, resp.headers.get("Content-Type"), len(data), data[:8].hex()
    except Exception as ex:
        body=b""
        try: body=ex.read()[:200]
        except Exception: pass
        return getattr(ex,"code",None), None, 0, body

for i in [71,72,73,74,10]:
    print("API", i, get(f"http://127.0.0.1:5301/api/sales-manager/customer-documents/{i}/file"))

# inspect DB row FileKey — use sql if available
cs = ""
for item in env:
    if b"ConnectionString" in item or b"DefaultConnection" in item:
        print("ENVCS", item.split(b"=",1)[0].decode())
app = Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json")
if app.exists():
    t = app.read_text(encoding="utf-8", errors="replace")
    if "ConnectionStrings" in t:
        print("has connection strings")
# try querying via a small csharp? or mssql
# Use profile docs metadata from company
req = urllib.request.Request("http://127.0.0.1:5301/api/sales-manager/customers/53344/profile", headers=headers)
with urllib.request.urlopen(req, timeout=30) as resp:
    prof = json.loads(resp.read().decode())
docs = prof.get("customerDocuments") or []
for d in docs:
    print("META", json.dumps({k:d.get(k) for k in d}, ensure_ascii=False)[:300])
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc7.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc7.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
