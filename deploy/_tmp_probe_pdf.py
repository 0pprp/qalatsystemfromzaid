#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import subprocess, sys
from pathlib import Path
pdf = Path("/opt/qalaa-runtime/AdminNajaf/App_Data/sales/24/Sale_24_SaleDocuments.pdf")
# try pypdf / PyPDF2 / fitz
for mod in ["fitz", "pymupdf", "pypdf", "PyPDF2", "pdf2image"]:
    try:
        __import__(mod if mod!="pymupdf" else "fitz")
        print("HAS_MOD", mod)
    except Exception:
        print("NO_MOD", mod)

# raw scan for JPEG markers in PDF
data = pdf.read_bytes()
print("PDF_LEN", len(data))
starts = []
i = 0
while True:
    j = data.find(b"\xff\xd8\xff", i)
    if j < 0: break
    starts.append(j)
    i = j+3
print("JPEG_MARKERS", len(starts), starts[:10])
# extract each jpeg until EOI
outdir = Path("/tmp/sale24_jpg")
outdir.mkdir(exist_ok=True)
extracted = []
for n, start in enumerate(starts):
    end = data.find(b"\xff\xd9", start)
    if end < 0: continue
    end += 2
    blob = data[start:end]
    if len(blob) < 5000: continue
    path = outdir / f"embed_{n}.jpg"
    path.write_bytes(blob)
    extracted.append((path.name, len(blob)))
print("EXTRACTED", extracted)

# if we got 4 images, restore as 71-74
live = Path("/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs")
if len(extracted) >= 4:
    for idx, doc_id in enumerate([71,72,73,74]):
        src = outdir / extracted[idx][0]
        dst = live / f"{doc_id}.jpg"
        if not dst.exists():
            dst.write_bytes(src.read_bytes())
            print("RESTORED", dst, dst.stat().st_size)
        else:
            print("EXISTS", dst)
elif extracted:
    print("ONLY", len(extracted), "images — not auto-restoring")
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_pdf.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_pdf.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
