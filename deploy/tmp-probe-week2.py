#!/usr/bin/env python3
import os, sys
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REMOTE = r'''#!/usr/bin/env python3
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
open("/tmp/week_probe_out.txt","w",encoding="utf-8").write(f"DB={db}\n")

def run(sql, tag):
    try:
        out=subprocess.check_output([SQLCMD,"-S",server,"-d",db,"-U",user,"-P",pwd,"-C","-W","-Q",sql], stderr=subprocess.STDOUT, text=True, timeout=120)
        with open("/tmp/week_probe_out.txt","a",encoding="utf-8") as f:
            f.write(f"\n==== {tag} ====\n")
            f.write(out[-5000:])
    except subprocess.CalledProcessError as e:
        with open("/tmp/week_probe_out.txt","a",encoding="utf-8") as f:
            f.write(f"\n==== {tag} FAIL ====\n")
            f.write((e.output or str(e))[-5000:])

run("""SET NOCOUNT ON;
SELECT COUNT(*) AS ViewCount FROM dbo.View_CustomerWeekPaymentDevice;
SELECT COL_LENGTH(N'dbo.View_CustomerWeekPaymentDevice', N'IsFakeSale') AS HasFake;
SELECT TOP 10 DelegateID, DelegateName FROM dbo.Delegates WHERE DelegateName LIKE N'%جزائر%' OR DelegateName LIKE N'%الجزائر%' OR DelegateName LIKE N'%اولي%';
""","meta")

run("""SET NOCOUNT ON;
DECLARE @d INT = (SELECT TOP 1 DelegateID FROM dbo.Delegates WHERE DelegateName LIKE N'%جزائر%');
SELECT @d AS DelegateId;
SELECT COUNT(*) AS Cnt FROM dbo.View_CustomerWeekPaymentDevice WHERE DelegateID=@d AND ISNULL(IsFakeSale,0)=0;
EXEC dbo.Customers_GetWeekReceipt @DelegateID=@d, @ShowType=N'الجميع';
""","exec_algeria")

run("""SET NOCOUNT ON;
SELECT COUNT(*) AS CntNull FROM dbo.View_CustomerWeekPaymentDevice WHERE ISNULL(IsFakeSale,0)=0;
EXEC dbo.Customers_GetWeekReceipt @DelegateID=NULL, @ShowType=N'الجميع';
""","exec_null")

run("""SET NOCOUNT ON;
-- simulate FE sending 0
EXEC dbo.Customers_GetWeekReceipt @DelegateID=0, @ShowType=N'الجميع';
SELECT COUNT(*) AS Cnt0 FROM dbo.View_CustomerWeekPaymentDevice WHERE DelegateID=0 AND ISNULL(IsFakeSale,0)=0;
""","exec_zero")

# live HTTP against najaf unit port
import re
cat=subprocess.getoutput("systemctl cat qalaa-company-sharenewnajaf")
m=re.search(r"ASPNETCORE_URLS=([^\s\"]+)", cat) or re.search(r"--urls[= ]([^\s]+)", cat)
urls=m.group(1) if m else ""
with open("/tmp/week_probe_out.txt","a",encoding="utf-8") as f:
    f.write(f"\nURLS={urls}\n")
# Environment file
env=subprocess.getoutput("systemctl show qalaa-company-sharenewnajaf -p Environment --no-pager")
with open("/tmp/week_probe_out.txt","a",encoding="utf-8") as f:
    f.write(env[:1500]+"\n")
print("WROTE")
'''

c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
with c.open_sftp() as sftp:
    with sftp.file("/tmp/probe_week2.py","w") as f: f.write(REMOTE)
_,o,e=c.exec_command("python3 /tmp/probe_week2.py", timeout=180)
print(o.read().decode("utf-8","replace"))
print(e.read().decode("utf-8","replace")[:500])
_,o2,_=c.exec_command("cat /tmp/week_probe_out.txt", timeout=30)
print(o2.read().decode("utf-8","replace"))
c.close()
