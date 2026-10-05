#!/usr/bin/env python3
import json
import os
import subprocess
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, os, subprocess
from pathlib import Path

print("DLL_FOLLOWER", b"ListFollowersAsync" in Path("/opt/qalaa-runtime/AdminNajaf/BE_Company.dll").read_bytes())
print("DLL_SERVICE", b"SalesManagerFollowerQueryService" in Path("/opt/qalaa-runtime/AdminNajaf/BE_Company.dll").read_bytes())

d=json.load(open("/opt/qalaa-runtime/AdminNajaf/appsettings.json", encoding="utf-8"))
print("FollowerTracking", d.get("FollowerTracking"))
print("CS_KEYS", list((d.get("ConnectionStrings") or {}).keys()))
cs = (d.get("ConnectionStrings") or {}).get("DataBaseConnection") or (d.get("ConnectionStrings") or {}).get("SalesDemoConnection") or ""
# parse Database=
db=""
for part in cs.replace(";", " ").split():
    if part.lower().startswith("database="):
        db=part.split("=",1)[1]
print("DB", db)

# get SA password from env file if any
pw=""
for envp in ["/etc/qalaa-production/company-najaf.env","/opt/qalaa-runtime/AdminNajaf/.env"]:
    if os.path.isfile(envp):
        for line in open(envp, encoding="utf-8", errors="replace"):
            if "SA_PASSWORD" in line or "MSSQL_SA" in line or "SqlPassword" in line:
                print("ENVHIT", envp, line.split("=")[0])

# try sql with sqlcmd using connection from appsettings - extract password
import re
m=re.search(r"Password=([^;]+)", cs, re.I)
user_m=re.search(r"User ID=([^;]+)", cs, re.I) or re.search(r"Uid=([^;]+)", cs, re.I)
server_m=re.search(r"Server=([^;]+)", cs, re.I) or re.search(r"Data Source=([^;]+)", cs, re.I)
if m and user_m and server_m and db:
    server=server_m.group(1)
    user=user_m.group(1)
    password=m.group(1)
    q="SET NOCOUNT ON; SELECT COUNT(1) AS C FROM dbo.Users WHERE ISNULL(UserState,1)=1 AND (UserType=N'متابع' OR UserType LIKE N'متابع%'); SELECT TOP 5 UserID, UserName, UserType FROM dbo.Users WHERE ISNULL(UserState,1)=1 AND (UserType=N'متابع' OR UserType LIKE N'متابع%');"
    cmd=["/opt/mssql-tools18/bin/sqlcmd","-S",server,"-U",user,"-P",password,"-d",db,"-C","-Q",q,"-W","-s","|"]
    if not Path(cmd[0]).exists():
        cmd[0]="sqlcmd"
    out=subprocess.run(cmd, capture_output=True, text=True)
    print("SQL_OUT", out.stdout[:800])
    print("SQL_ERR", out.stderr[:400])
else:
    print("NO_SQL_CREDS")

# curl local branch followers endpoint (no auth - expect 401)
print("CURL_LOCAL")
print(subprocess.getoutput("curl -s -o /tmp/f.json -w '%{http_code}' http://127.0.0.1:$(systemctl show qalaa-company-sharenewnajaf.service -p Environment --value 2>/dev/null | tr ' ' '\\n' | grep -i ASPNETCORE_URLS | head -1) 2>/dev/null; ss -lntp | grep -i AdminNajaf | head -3; ss -lntp | grep qalaa-company-sharenewnajaf | head -3"))
# find listen port
print(subprocess.getoutput("systemctl show qalaa-company-sharenewnajaf.service -p Environment --value | tr ' ' '\\n' | grep -i URL"))
print("JOURNAL")
print(subprocess.getoutput("journalctl -u qalaa-company-sharenewnajaf.service -n 50 --no-pager -o cat | tail -50"))
'''

def main():
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_followers.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_followers.py", timeout=120)
    print(out.read().decode("utf-8", "replace"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e[-1500:])
    c.close()

if __name__ == "__main__":
    main()
