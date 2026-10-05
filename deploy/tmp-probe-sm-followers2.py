#!/usr/bin/env python3
import os
import re

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, os, re, subprocess
from pathlib import Path

sqlcmd = "/opt/mssql-tools18/bin/sqlcmd"
if not Path(sqlcmd).exists():
    sqlcmd = "sqlcmd"

def parse_cs(cs):
    def g(pat):
        m = re.search(pat, cs, re.I)
        return m.group(1) if m else None
    return g(r"Server=([^;]+)") or g(r"Data Source=([^;]+)"), g(r"User ID=([^;]+)") or g(r"Uid=([^;]+)"), g(r"Password=([^;]+)"), g(r"Database=([^;]+)") or g(r"Initial Catalog=([^;]+)")

admins = sorted(Path("/opt/qalaa-runtime").glob("Admin*"))
admins = [a for a in admins if a.is_dir() and "Frontend" not in a.name and "backup" not in a.name.lower()]
print("ADMINS", len(admins))
total = 0
for a in admins:
    cfg = a / "appsettings.json"
    if not cfg.exists():
        continue
    d = json.loads(cfg.read_text(encoding="utf-8"))
    cs = (d.get("ConnectionStrings") or {}).get("DataBaseConnection") or ""
    server, user, pw, db = parse_cs(cs)
    if not all([server, user, pw, db]):
        print("SKIP", a.name, "badcs", bool(cs), db)
        continue
    q = """SET NOCOUNT ON;
SELECT COUNT(1) FROM dbo.Users WHERE ISNULL(UserState,1)=1 AND (UserType=N'متابع' OR UserType LIKE N'متابع%');
SELECT TOP 3 UserID, UserName, UserType FROM dbo.Users WHERE ISNULL(UserState,1)=1 AND (UserType=N'متابع' OR UserType LIKE N'متابع%');
SELECT DISTINCT TOP 20 UserType FROM dbo.Users WHERE UserType LIKE N'%تابع%' OR UserType LIKE N'%follow%';"""
    r = subprocess.run([sqlcmd,"-S",server,"-U",user,"-P",pw,"-d",db,"-C","-Q",q,"-W","-h","-1"], capture_output=True, text=True)
    out = (r.stdout or "").strip().replace("\r","")
    lines = [ln.strip() for ln in out.splitlines() if ln.strip()]
    print("==", a.name, "db=", db)
    print("OUT", " | ".join(lines[:12]))
    if r.stderr:
        print("ERR", r.stderr[:200])
    # first line count
    if lines:
        try:
            total += int(lines[0].split()[0])
        except Exception:
            pass

print("TOTAL_MUTABA_ROWS", total)

# soft fanout timing: hit local najaf followers without auth expect 401/403
print("LOCAL", subprocess.getoutput("curl -s -o /tmp/fr.json -w '%{http_code}' http://127.0.0.1:5301/api/sales-manager/followers"))
print("BODY", open("/tmp/fr.json",encoding="utf-8",errors="replace").read()[:300])

# check Production/env for FollowerTracking
for p in ["/opt/qalaa-runtime/AdminNajaf/appsettings.Production.json","/etc/qalaa-production/company-najaf.env"]:
    if Path(p).exists():
        txt = Path(p).read_text(encoding="utf-8", errors="replace")
        print("FILE", p, "FollowerTracking" in txt or "DelegateApiBase" in txt)
        if "DelegateApiBase" in txt:
            for line in txt.splitlines():
                if "Delegate" in line or "FollowerTracking" in line or "AccountantKey" in line:
                    # redact key values
                    if "Key" in line or "Password" in line:
                        print(" ", line.split("=")[0] if "=" in line else line[:40], "=***")
                    else:
                        print(" ", line[:120])
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/probe_followers2.py", "w") as f:
    f.write(REMOTE)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/probe_followers2.py", timeout=180)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("STDERR", e[-800:])
c.close()
