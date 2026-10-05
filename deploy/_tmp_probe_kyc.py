#!/usr/bin/env python3
"""Probe Najaf KYC file via company API + gateway director path."""
from __future__ import annotations

import base64
import json
import os
import re
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, re, pathlib, subprocess, urllib.request, ssl
from pathlib import Path

# locate gateway key
key = ""
for p in [
    "/opt/qalaa-runtime/SalesGateway/appsettings.json",
    "/opt/qalaa-runtime/SalesGateway/appsettings.Production.json",
    "/opt/qalaa-runtime/BE_SalesEmployee/appsettings.json",
    "/opt/qalaa-runtime/BE_SalesEmployee/appsettings.Production.json",
]:
    path = Path(p)
    if not path.exists():
        continue
    t = path.read_text(encoding="utf-8", errors="replace")
    print("FILE", p)
    for pat in [r'"InternalApiKey"\s*:\s*"([^"]+)"', r'"GatewayKey"\s*:\s*"([^"]+)"']:
        m = re.search(pat, t)
        if m:
            key = m.group(1)
            print("KEY_FOUND", pat, "len", len(key))
            break
    if key:
        break

# najaf working dir / port
unit = subprocess.check_output(["systemctl", "cat", "qalaa-company-sharenewnajaf"], text=True, errors="replace")
print("UNIT_SNIP")
for line in unit.splitlines():
    if any(x in line for x in ("ExecStart", "WorkingDirectory", "ASPNETCORE_URLS", "Environment=")):
        print(line)

# try common local ports from ss
ss = subprocess.check_output(["ss", "-lntp"], text=True, errors="replace")
for line in ss.splitlines():
    if "AdminNajaf" in line or "sharenewnajaf" in line or "BE_Company" in line:
        print("SS", line)

# find city value for najaf in gateway config / cities
for p in Path("/opt/qalaa-runtime").rglob("appsettings*.json"):
    if "SalesGateway" not in str(p) and "SalesEmployee" not in str(p) and "BE_Sales" not in str(p):
        continue
    t = p.read_text(encoding="utf-8", errors="replace")
    if "Najaf" in t or "najaf" in t:
        print("CFG", p)
        for m in re.finditer(r'.{0,40}[Nn]ajaf.{0,80}', t):
            print(" ", m.group(0).replace("\n"," ")[:120])

doc_id = 10
name_b64 = __import__("base64").b64encode(b"probe").decode()
headers = {
    "X-Sales-Gateway-Key": key,
    "X-Sales-Manager-Name-B64": name_b64,
}

# try curl to local company on typical urls from service
ports = re.findall(r":(\d+)", "\n".join(l for l in unit.splitlines() if "URL" in l or "urls" in l.lower() or "518" in l or "5" in l))
print("PORTS_FROM_UNIT", ports)

# parse ExecStart for --urls
m = re.search(r"--urls[= ](\S+)", unit)
urls = m.group(1) if m else ""
print("URLS", urls)

candidates = []
if urls:
    for u in urls.split(";"):
        candidates.append(u.rstrip("/") + f"/api/sales-manager/customer-documents/{doc_id}/file")
# fallback common
for host in ["http://127.0.0.1:5180", "http://127.0.0.1:5080", "http://127.0.0.1:5000"]:
    candidates.append(host + f"/api/sales-manager/customer-documents/{doc_id}/file")

# also resolve from WorkingDirectory env file
wd = None
for line in unit.splitlines():
    if line.strip().startswith("WorkingDirectory="):
        wd = line.split("=",1)[1].strip()
print("WD", wd)

seen=set()
for url in candidates:
    if url in seen: continue
    seen.add(url)
    try:
        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req, timeout=20) as resp:
            data = resp.read(32)
            ctype = resp.headers.get("Content-Type","")
            print("OK", url, "status", resp.status, "ctype", ctype, "magic", data[:8].hex(), "lenhint", resp.headers.get("Content-Length"))
            break
    except Exception as ex:
        print("FAIL", url, type(ex).__name__, ex)

# gateway director probe — find gateway listen
gw_unit = subprocess.check_output(["systemctl", "cat", "qalaa-sales-gateway"], text=True, errors="replace")
print("GW_UNIT")
for line in gw_unit.splitlines():
    if any(x in line for x in ("ExecStart", "WorkingDirectory", "ASPNETCORE_URLS", "Environment=")):
        print(line)
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
        with sftp.file("/tmp/probe_kyc.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:2000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
