#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, urllib.request
from urllib.parse import quote

def call(method, url, headers=None, body=None, timeout=90):
    data = None if body is None else (body if isinstance(body, (bytes, bytearray)) else json.dumps(body).encode())
    req = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    if body is not None and "Content-Type" not in (headers or {}):
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.status, dict(resp.headers), resp.read(), None
    except Exception as ex:
        b = b""
        try: b = ex.read()
        except Exception: pass
        return getattr(ex, "code", None), {}, b, str(ex)

# director login bootstrap
status, hdr, data, err = call("POST", "http://127.0.0.1:5280/api/Auth/LoginDirector", body={"userName": "حيدر", "password": "حيدر"})
print("DIR_LOGIN", status, data[:200] if data else err)
token = None
if status == 200:
    token = json.loads(data.decode()).get("token")
auth = {"Authorization": f"Bearer {token}"} if token else {}

status, hdr, data, err = call("GET", "http://127.0.0.1:5280/api/director/cities", headers=auth)
print("CITIES", status, data[:250] if data else err)

# document 10 via director
status, hdr, data, err = call("GET", "http://127.0.0.1:5280/api/director/customer-documents/1/10/file", headers=auth)
ctype = hdr.get("Content-Type") if hdr else None
print("DIR_DOC", status, ctype, "bytes", len(data) if data else 0, "magic", (data[:12].hex() if data and status==200 else data[:250]))

# shop image sanity if any
status, hdr, data, err = call("GET", "http://127.0.0.1:5280/api/director/customers/1/123/profile", headers=auth)
print("PROFILE", status, data[:300] if data else err)
if status == 200:
    prof = json.loads(data.decode())
    docs = prof.get("customerDocuments") or prof.get("CustomerDocuments") or []
    print("DOCS_COUNT", len(docs))
    for d in docs[:6]:
        print("DOC", d.get("id") or d.get("Id"), d.get("documentType") or d.get("DocumentType"), d.get("contentType") or d.get("ContentType"))
        did = d.get("id") or d.get("Id")
        if did:
            st2, h2, b2, e2 = call("GET", f"http://127.0.0.1:5280/api/director/customer-documents/1/{did}/file", headers=auth)
            print("  FILE", st2, h2.get("Content-Type") if h2 else None, len(b2) if b2 else 0, (b2[:8].hex() if b2 and st2==200 else b2[:120]))

# Ahmed from screenshot phone
status, hdr, data, err = call("GET", f"http://127.0.0.1:5280/api/director/customers/1/0/profile?phone=07718767112", headers=auth)
# try search then profile
status, hdr, data, err = call("GET", "http://127.0.0.1:5280/api/sales-manager/customers/search?cityValue=1&q="+quote("احمد عماد"), headers={"Authorization": auth.get("Authorization","x")})
# director may not have search — use SM token for find id
payload = json.dumps({"userName":"karrar","password":"karrar123"}).encode()
req = urllib.request.Request("http://127.0.0.1:5280/api/Auth/LoginSalesManager", data=payload, headers={"Content-Type":"application/json"})
with urllib.request.urlopen(req, timeout=20) as resp:
    sm = json.loads(resp.read().decode())["token"]
status, hdr, data, err = call("GET", "http://127.0.0.1:5280/api/sales-manager/customers/search?cityValue=1&q="+quote("07718767112"), headers={"Authorization": f"Bearer {sm}"})
print("FIND", status, data[:400] if data else err)
if status == 200:
    rows = json.loads(data.decode())
    if rows:
        cid = rows[0].get("customerId") or rows[0].get("CustomerId")
        print("CID", cid)
        status, hdr, data, err = call("GET", f"http://127.0.0.1:5280/api/director/customers/1/{cid}/profile", headers=auth)
        print("AHMED_PROFILE", status, data[:200] if data else err)
        if status == 200:
            prof = json.loads(data.decode())
            docs = prof.get("customerDocuments") or []
            print("AHMED_DOCS", len(docs))
            for d in docs:
                did = d.get("id") or d.get("Id")
                st2, h2, b2, e2 = call("GET", f"http://127.0.0.1:5280/api/director/customer-documents/1/{did}/file", headers=auth)
                print("  AFILE", did, st2, h2.get("Content-Type") if h2 else None, len(b2) if b2 else 0, (b2[:8].hex() if b2 and st2==200 else b2[:150]))
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc6.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc6.py", timeout=180)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
