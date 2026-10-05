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

# key from service env
unit = subprocess.check_output(["systemctl", "show", "qalaa-company-sharenewnajaf", "-p", "Environment", "--value"], text=True, errors="replace")
key = ""
for part in unit.split():
    if part.startswith("SalesEmployee__GatewayKey="):
        key = part.split("=",1)[1].strip().strip('"')
print("KEY_LEN", len(key))

doc_id = 10
headers = {
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name-B64": base64.b64encode("director-probe".encode()).decode(),
}
url = f"http://127.0.0.1:5301/api/sales-manager/customer-documents/{doc_id}/file"
try:
    req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=30) as resp:
        data = resp.read()
        print("COMPANY", resp.status, resp.headers.get("Content-Type"), "bytes", len(data), "magic", data[:8].hex())
except Exception as ex:
    print("COMPANY_FAIL", type(ex).__name__, ex)
    if hasattr(ex, "read"):
        try:
            print("BODY", ex.read()[:300])
        except Exception:
            pass
    if hasattr(ex, "code"):
        try:
            body = ex.read()
            print("HTTP", ex.code, body[:300])
        except Exception as e2:
            print("HTTP_ERR", e2)

# find najaf city value from GetAdmin or gateway cities cache
gw_unit = subprocess.check_output(["systemctl", "show", "qalaa-sales-gateway", "-p", "Environment", "--value"], text=True, errors="replace")
internal = ""
for part in gw_unit.split():
    if part.startswith("InternalApiKey="):
        internal = part.split("=",1)[1].strip().strip('"')
print("INTERNAL_LEN", len(internal))

# login director — find director accounts
for part in gw_unit.split():
    if "Director" in part:
        print("DIR_ENV", part[:80])

# try cities endpoint with a director login if we know credentials from appsettings
cfg = Path("/opt/qalaa-runtime/SalesGateway/appsettings.json")
if cfg.exists():
    t = cfg.read_text(encoding="utf-8", errors="replace")
    if "Director" in t:
        print("CFG_HAS_DIRECTOR")
        for m in re.finditer(r'Director.{0,200}', t):
            print(m.group(0)[:180])

# probe gateway SM path with known SM account for same file (central)
login_url = "http://127.0.0.1:5280/api/Auth/LoginSalesManager"
payload = json.dumps({"userName": "karrar", "password": "karrar123"}).encode()
req = urllib.request.Request(login_url, data=payload, headers={"Content-Type": "application/json"})
try:
    with urllib.request.urlopen(req, timeout=20) as resp:
        login = json.loads(resp.read().decode())
        token = login.get("token")
        print("SM_LOGIN", bool(token), login.get("userType"), login.get("central"))
except Exception as ex:
    print("SM_LOGIN_FAIL", ex)
    token = None

# get cities to find najaf value
if token:
    req = urllib.request.Request(
        "http://127.0.0.1:5280/api/sales-manager/cities",
        headers={"Authorization": f"Bearer {token}"},
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            cities = json.loads(resp.read().decode())
            najaf = None
            for c in cities if isinstance(cities, list) else []:
                name = str(c.get("name") or c.get("Name") or "")
                val = str(c.get("value") or c.get("Value") or "")
                if "نجف" in name or "Najaf" in val or "najaf" in val.lower():
                    najaf = c
                    print("NAJAF", name, val)
            if not najaf and cities:
                print("CITY0", cities[0])
    except Exception as ex:
        print("CITIES_FAIL", ex)
        najaf = None

    city = None
    if najaf:
        city = najaf.get("value") or najaf.get("Value")
    if city:
        for path in [
            f"http://127.0.0.1:5280/api/sales-manager/customer-documents/{city}/{doc_id}/file",
            f"http://127.0.0.1:5280/api/director/customer-documents/{city}/{doc_id}/file",
        ]:
            req = urllib.request.Request(path, headers={"Authorization": f"Bearer {token}"})
            try:
                with urllib.request.urlopen(req, timeout=60) as resp:
                    data = resp.read()
                    print("GW", path.split("/api/")[-1], resp.status, resp.headers.get("Content-Type"), "bytes", len(data), "magic", data[:8].hex())
            except Exception as ex:
                body = b""
                code = getattr(ex, "code", "?")
                try:
                    body = ex.read()[:400]
                except Exception:
                    pass
                print("GW_FAIL", path.split("/api/")[-1], code, body[:200])

# director login probe
for user, pwd in [("admin", "admin"), ("director", "director"), ("مدير", "123")]:
    pass

# find DirectorAccount in dll/config
for p in Path("/opt/qalaa-runtime/SalesGateway").glob("appsettings*.json"):
    t = p.read_text(encoding="utf-8", errors="replace")
    print("SETTINGS", p.name, "Director" in t)
    for m in re.finditer(r'"Director[^"]*"\s*:\s*\{[^}]+\}', t, re.S):
        print(m.group(0)[:300])
'''

def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc2.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc2.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:2000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
