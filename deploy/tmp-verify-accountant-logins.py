#!/usr/bin/env python3
"""Smoke-test jana/hadi LoginAdmin against a sample of province APIs."""
from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, urllib.request, urllib.error

ACCOUNTS = [("jana","jana1212"), ("hadi","hadi2006")]
# sample of live hostnames from typical Admin folders
TARGETS = [
  ("Najaf", "https://sharenewnajaf.alsaaeidy.com/api/Users/Users_LoginAdmin"),
  ("Basra", "https://sharenewrbasra.alsaaeidy.com/api/Users/Users_LoginAdmin"),
  ("Karbala", "https://sharenewkarbala.alsaaeidy.com/api/Users/Users_LoginAdmin"),
  ("Hilla", "https://sharenewrbabil.alsaaeidy.com/api/Users/Users_LoginAdmin"),
  ("Mosul", "https://sharenewmusol.alsaaeidy.com/api/Users/Users_LoginAdmin"),
]

for label, url in TARGETS:
    for user, pwd in ACCOUNTS:
        body = json.dumps({"userName": user, "password": pwd}).encode()
        req = urllib.request.Request(url, data=body, headers={"Content-Type":"application/json"}, method="POST")
        try:
            with urllib.request.urlopen(req, timeout=25) as r:
                data = json.loads(r.read().decode("utf-8","replace"))
            ok = bool(data.get("token"))
            msg = data.get("message") or ""
            print(f"{label}|{user}|{'OK' if ok else 'FAIL'}|{msg[:80]}")
        except Exception as ex:
            print(f"{label}|{user}|ERR|{ex}")
print("DONE")
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    with c.open_sftp() as sftp:
        with sftp.file("/tmp/verify_acct_login.py", "w") as f:
            f.write(REMOTE)
    _, o, e = c.exec_command("python3 /tmp/verify_acct_login.py", timeout=180)
    print(o.read().decode("utf-8", "replace"))
    err = e.read().decode("utf-8", "replace")
    if err.strip():
        print("SSH_ERR", err[-500:])
    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
