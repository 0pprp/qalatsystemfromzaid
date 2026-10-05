#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import base64, json, re, subprocess, urllib.request
from pathlib import Path

unit = subprocess.check_output(["systemctl", "show", "qalaa-company-sharenewnajaf", "-p", "Environment", "--value"], text=True, errors="replace")
key = ""
for part in unit.split():
    if part.startswith("SalesEmployee__GatewayKey="):
        key = part.split("=",1)[1].strip().strip('"')

# also read appsettings InternalApiKey / GatewayKey
for p in [Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json"), Path("/opt/qalaa-runtime/AdminNajaf/appsettings.Production.json")]:
    if p.exists():
        t = p.read_text(encoding="utf-8", errors="replace")
        print("APP", p.name)
        for pat in ["InternalApiKey", "GatewayKey", "SalesEmployee"]:
            if pat in t:
                print(" has", pat)
        for m in re.finditer(r'"(InternalApiKey|GatewayKey)"\s*:\s*"([^"]*)"', t):
            print(" cfg", m.group(1), "len", len(m.group(2)), "match_env", m.group(2)==key)

doc_id = 10
url = f"http://127.0.0.1:5301/api/sales-manager/customer-documents/{doc_id}/file"

def try_req(label, headers):
    req = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = resp.read(16)
            print(label, "OK", resp.status, resp.headers.get("Content-Type"), data[:8].hex())
    except Exception as ex:
        body = b""
        code = getattr(ex, "code", None)
        try:
            body = ex.read()[:250]
        except Exception:
            pass
        print(label, "FAIL", code, body[:200])

try_req("key_only", {"X-Sales-Gateway-Key": key})
try_req("key_b64", {
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name-B64": base64.b64encode("SalesManager".encode()).decode(),
})
try_req("key_name", {
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name": "SalesManager",
})
try_req("wrong_key", {"X-Sales-Gateway-Key": "deadbeef"})

# profile endpoint same auth
purl = "http://127.0.0.1:5301/api/sales-manager/customers/1/profile"
req = urllib.request.Request(purl, headers={
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name-B64": base64.b64encode("SalesManager".encode()).decode(),
})
try:
    with urllib.request.urlopen(req, timeout=30) as resp:
        print("PROFILE", resp.status, resp.read(120)[:120])
except Exception as ex:
    body=b""
    try: body=ex.read()[:200]
    except Exception: pass
    print("PROFILE_FAIL", getattr(ex,"code",None), body[:200])

# SM central via gateway — find cities path
login_url = "http://127.0.0.1:5280/api/Auth/LoginSalesManager"
payload = json.dumps({"userName": "karrar", "password": "karrar123"}).encode()
req = urllib.request.Request(login_url, data=payload, headers={"Content-Type": "application/json"})
with urllib.request.urlopen(req, timeout=20) as resp:
    token = json.loads(resp.read().decode())["token"]

for path in [
    "http://127.0.0.1:5280/api/sales-manager/branches",
    "http://127.0.0.1:5280/api/sales-manager/cities",
    "http://127.0.0.1:5280/api/director/cities",
]:
    req = urllib.request.Request(path, headers={"Authorization": f"Bearer {token}"})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read()
            print("PATH", path.split("/api/")[-1], resp.status, raw[:180])
    except Exception as ex:
        body=b""
        try: body=ex.read()[:120]
        except Exception: pass
        print("PATH_FAIL", path.split("/api/")[-1], getattr(ex,"code",None), body[:120])

# director login
for user,pwd in [("karrar","karrar123"),("director","director"),("admin","admin123")]:
    for ep in ["LoginDirector", "LoginSalesManager"]:
        req = urllib.request.Request(
            f"http://127.0.0.1:5280/api/Auth/{ep}",
            data=json.dumps({"userName": user, "password": pwd}).encode(),
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = json.loads(resp.read().decode())
                if data.get("token"):
                    print("LOGIN_OK", ep, user, data.get("userType"), data.get("central"))
        except Exception:
            pass
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc3.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc3.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
