#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import base64, json, urllib.request

pid = __import__("subprocess").check_output(["systemctl","show","qalaa-company-sharenewnajaf","-p","MainPID","--value"], text=True).strip()
env = open(f"/proc/{pid}/environ","rb").read().split(b"\0")
key=""
for item in env:
    if item.startswith(b"SalesEmployee__GatewayKey="):
        key=item.split(b"=",1)[1].decode()
headers={"X-Sales-Gateway-Key":key,"X-Sales-Manager-Name-B64":base64.b64encode(b"SalesManager").decode()}

# profile already has docs 71-74. Check if any file among missing range exists for same name in other branches? skip.

# Try extract images from Sale_24_SaleDocuments.pdf via pdfimages if available
import subprocess
from pathlib import Path
pdf = Path("/opt/qalaa-runtime/AdminNajaf/App_Data/sales/24/Sale_24_SaleDocuments.pdf")
print("PDF", pdf.exists(), pdf.stat().st_size)
out = Path("/tmp/sale24_imgs")
if out.exists():
    import shutil; shutil.rmtree(out)
out.mkdir()
for tool in ["pdfimages", "pdftoppm"]:
    try:
        subprocess.check_call(["which", tool])
        print("HAS", tool)
    except Exception:
        print("NO", tool)

try:
    subprocess.check_call(["pdfimages", "-all", str(pdf), str(out/"img")], timeout=30)
    print("EXTRACTED", list(out.iterdir()))
except Exception as ex:
    print("pdfimages fail", ex)
    try:
        subprocess.check_call(["pdftoppm", "-png", str(pdf), str(out/"page")], timeout=30)
        print("PAGES", list(out.iterdir()))
    except Exception as ex2:
        print("pdftoppm fail", ex2)

# how many DB docs missing files in Najaf — sample via API for ids in missing list
missing = [71,72,73,74,63,64,65]
for i in missing:
    req=urllib.request.Request(f"http://127.0.0.1:5301/api/sales-manager/customer-documents/{i}/file", headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            print("OK", i, r.status, r.headers.get("Content-Type"))
    except Exception as ex:
        print("MISS", i, getattr(ex,"code",None))
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc10.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc10.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
