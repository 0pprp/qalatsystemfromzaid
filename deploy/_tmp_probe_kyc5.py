#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''
import json, re, subprocess, urllib.request
from pathlib import Path
from urllib.parse import quote

# discover director accounts from env/config
gw = subprocess.check_output(["systemctl", "show", "qalaa-sales-gateway", "-p", "MainPID", "--value"], text=True).strip()
env = Path(f"/proc/{gw}/environ").read_bytes().split(b"\0")
pairs = {}
for item in env:
    if b"=" not in item: continue
    k,v = item.split(b"=",1)
    ks = k.decode(errors="replace")
    if "Director" in ks or "director" in ks:
        pairs[ks] = v.decode(errors="replace")
        print("ENV", ks, "=", "***" if "Pass" in ks or "Password" in ks else v[:40])

cfg = Path("/opt/qalaa-runtime/SalesGateway/appsettings.json")
if cfg.exists():
    t = cfg.read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r'Director[A-Za-z]*', t):
        pass
    # print keys only
    try:
        data = json.loads(t)
        def walk(obj, prefix=""):
            if isinstance(obj, dict):
                for k,v in obj.items():
                    if "irector" in k or "irector" in prefix:
                        if isinstance(v, (dict, list)):
                            print("CFG", prefix+k, type(v).__name__)
                            walk(v, prefix+k+".")
                        else:
                            val = str(v)
                            if "pass" in k.lower() or "password" in k.lower():
                                print("CFG", prefix+k, "len", len(val))
                            else:
                                print("CFG", prefix+k, val)
                    else:
                        walk(v, prefix+k+".")
            elif isinstance(obj, list):
                for i,v in enumerate(obj[:5]):
                    walk(v, prefix+f"[{i}].")
        walk(data)
    except Exception as ex:
        print("CFG_ERR", ex)

# try LoginDirector with common accounts from unit file earlier — and from DirectorAccount section
candidates = []
for item in env:
    if not item.startswith(b"Director"): continue
print("trying logins...")

# strings in dll
dll = Path("/opt/qalaa-runtime/SalesGateway/BE_SalesEmployee.dll").read_bytes()
for s in [b"DirectorAccount", b"LoginDirector", b"UserName"]:
    print("DLL", s, s in dll)

# dump DirectorAccountService related config section via env listing
for item in sorted(env):
    ks = item.split(b"=",1)[0].decode(errors="replace")
    if "Account" in ks or "irect" in ks.lower():
        print("K", ks)
'''

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/probe_kyc5.py", "w") as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command("python3 /tmp/probe_kyc5.py", timeout=120)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:1000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
