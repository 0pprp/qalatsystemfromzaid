#!/usr/bin/env python3
import os, sys, paramiko
HOST="169.58.236.52"
SSH_PASS=os.environ["SSH_PASS"]
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REMOTE=r'''
import json, subprocess
from pathlib import Path
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
cfg=json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8"))
cs=cfg["ConnectionStrings"]["DataBaseConnection"]
parts={}
for p in cs.split(";"):
    if "=" in p:
        k,v=p.split("=",1); parts[k.strip().lower()]=v.strip()
server=parts.get("server") or parts.get("data source")
db=parts.get("database") or parts.get("initial catalog")
user=parts.get("user id") or parts.get("uid")
pwd=parts.get("password") or parts.get("pwd")
sql="""SET NOCOUNT ON;
SELECT TOP 5 UserID, UserName, Password, UserType, UserState, BoxID, Email, PhoneNumber
FROM dbo.Users WHERE UserType=N'محاسب رئيسي' ORDER BY UserID;
SELECT TOP 3 * FROM dbo.UsersSelectedCities;
SELECT CityID, CityName FROM dbo.Cities;
SELECT TOP 5 BoxID, BoxName FROM dbo.Boxes;
SELECT UserID, UserName FROM dbo.Users WHERE LOWER(UserName) IN ('jana','Jana');
"""
print(subprocess.check_output([SQLCMD,"-S",server,"-d",db,"-U",user,"-P",pwd,"-C","-W","-Q",sql], text=True, timeout=60))
'''
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=SSH_PASS,timeout=40,allow_agent=False,look_for_keys=False)
with c.open_sftp() as sftp:
    with sftp.file("/tmp/probe_acct_user.py","w") as f: f.write(REMOTE)
_,o,e=c.exec_command("python3 /tmp/probe_acct_user.py", timeout=60)
print(o.read().decode("utf-8","replace"))
print(e.read().decode("utf-8","replace")[:800])
c.close()
