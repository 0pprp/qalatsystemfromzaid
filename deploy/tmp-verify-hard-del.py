#!/usr/bin/env python3
import os, json, subprocess, paramiko
from pathlib import Path

HOST="169.58.236.52"
SSH_PASS=os.environ["SSH_PASS"]
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
REMOTE=r'''
import json, subprocess
from pathlib import Path
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
cfg=json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text())
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
SELECT 'UsersRemain' AS T, COUNT(*) AS C FROM dbo.Users WHERE LOWER(UserName) IN ('fat','zai','ban','jin','amar1');
SELECT 'UsersSM' AS T, UserID, UserName, UserType, ISNULL(UserState,1) AS S FROM dbo.Users WHERE UserType=N'مدير مبيعات';
SELECT 'Customers' AS T, COUNT(*) AS C FROM dbo.Customers;
SELECT 'Payments' AS T, COUNT(*) AS C FROM dbo.CustomersPayments;
SELECT 'Sales' AS T, COUNT(*) AS C FROM dbo.CustomersSales;
"""
print(subprocess.check_output([SQLCMD,"-S",server,"-d",db,"-U",user,"-P",pwd,"-C","-W","-Q",sql], text=True))
'''
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=SSH_PASS,timeout=40,allow_agent=False,look_for_keys=False)
with c.open_sftp() as sftp:
    with sftp.file("/tmp/verify_hard_del.py","w") as f: f.write(REMOTE)
_,o,e=c.exec_command("python3 /tmp/verify_hard_del.py", timeout=60)
print(o.read().decode("utf-8","replace"))
print(e.read().decode("utf-8","replace")[:500])
c.close()
