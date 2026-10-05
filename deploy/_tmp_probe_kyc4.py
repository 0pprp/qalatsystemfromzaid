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
from urllib.parse import quote

# read key from live process environ
pid = subprocess.check_output(["systemctl", "show", "qalaa-company-sharenewnajaf", "-p", "MainPID", "--value"], text=True).strip()
env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
key = ""
for item in env:
    if item.startswith(b"SalesEmployee__GatewayKey="):
        key = item.split(b"=",1)[1].decode()
print("PROC_KEY_LEN", len(key))

headers = {
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name-B64": base64.b64encode("SalesManager".encode()).decode(),
}

def get(url, hdrs, timeout=60):
    req = urllib.request.Request(url, headers=hdrs)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            data = resp.read()
            return resp.status, dict(resp.headers), data, None
    except Exception as ex:
        body = b""
        code = getattr(ex, "code", None)
        try:
            body = ex.read()
        except Exception:
            pass
        return code, {}, body, str(ex)

status, hdr, data, err = get("http://127.0.0.1:5301/api/sales-manager/customer-documents/10/file", headers)
print("COMPANY_DOC", status, hdr.get("Content-Type"), "bytes", len(data) if data else 0, "magic", (data[:8].hex() if data and status==200 else data[:120]))

# SM gateway login
payload = json.dumps({"userName": "karrar", "password": "karrar123"}).encode()
req = urllib.request.Request("http://127.0.0.1:5280/api/Auth/LoginSalesManager", data=payload, headers={"Content-Type":"application/json"})
with urllib.request.urlopen(req, timeout=20) as resp:
    token = json.loads(resp.read().decode())["token"]
auth = {"Authorization": f"Bearer {token}"}

# find customer by phone from screenshot
phone = "07718767112"
# try excel/search or profile
for path in [
    f"http://127.0.0.1:5280/api/sales-manager/customers/1/profile?customerId=0",
    f"http://127.0.0.1:5280/api/sales-manager/customers/profile?cityValue=1&phone={phone}",
]:
    # use aggregator path patterns from FE
    pass

# FE central SM profile path
paths = [
    f"http://127.0.0.1:5280/api/sales-manager/customers/1/profile?phone={phone}",
    f"http://127.0.0.1:5280/api/sales-manager/customers/profile/1?phone={phone}",
    f"http://127.0.0.1:5280/api/sales-manager/1/customers/profile?phone={phone}",
]
# check FE helper
print("probing profile routes...")
for p in [
    f"http://127.0.0.1:5280/api/sales-manager/customers/{quote('1')}/profile?phone={phone}",
]:
    # wrong - look at SalesManagerController gateway routes
    pass

# list gateway SM controller routes for profile
status, hdr, data, err = get(f"http://127.0.0.1:5280/api/sales-manager/customers/1/123/profile", auth)
print("probe1", status, data[:100] if data else err)

# Search customers
body = json.dumps({"q": "احمد عماد"}).encode()
# try GET search
status, hdr, data, err = get(f"http://127.0.0.1:5280/api/sales-manager/customers/search?cityValue=1&q={quote('احمد')}", auth)
print("SEARCH", status, (data[:300] if data else err))

# document via gateway SM
status, hdr, data, err = get("http://127.0.0.1:5280/api/sales-manager/customer-documents/1/10/file", auth, timeout=90)
print("GW_SM_DOC", status, hdr.get("Content-Type"), "bytes", len(data) if data else 0, "magic", (data[:8].hex() if data and status==200 else data[:200]))

# branch-month to confirm city 1 works
status, hdr, data, err = get("http://127.0.0.1:5280/api/sales-manager/stats/branch-month?cityValue=1", auth)
print("MONTH", status, data[:200] if data else err)
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc4.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc4.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
