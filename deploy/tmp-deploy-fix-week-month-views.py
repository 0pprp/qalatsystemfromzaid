#!/usr/bin/env python3
"""Deploy week/month view fix (RatingEquationFactor GROUP BY) to all province DBs."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
SQL_FILE = ROOT / "DatabaseScripts" / "Migrations" / "20260930_Fix_WeekMonthViews_RatingEquationFactor.sql"

ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    sql_text = SQL_FILE.read_text(encoding="utf-8")

    remote = r'''#!/usr/bin/env python3
import json, subprocess, tempfile
from pathlib import Path
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
ADMINS = __ADMINS__
sql = open("/tmp/fix_week_month_views.sql","r",encoding="utf-8").read()

def parse_cs(cs):
    parts={}
    for p in cs.split(";"):
        p=p.strip()
        if not p or "=" not in p: continue
        k,v=p.split("=",1); parts[k.strip().lower()]=v.strip()
    return (parts.get("server") or parts.get("data source"),
            parts.get("database") or parts.get("initial catalog"),
            parts.get("user id") or parts.get("uid") or parts.get("user"),
            parts.get("password") or parts.get("pwd"))

ok=fail=0
for name in ADMINS:
    cfg_path=Path(f"/opt/qalaa-runtime/{name}/appsettings.json")
    if not cfg_path.is_file():
        print("SKIP", name); continue
    try:
        cfg=json.loads(cfg_path.read_text(encoding="utf-8"))
        server,db,user,pwd=parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
    except Exception as e:
        print("CFG_ERR", name, e); fail+=1; continue
    try:
        out=subprocess.check_output(
            [SQLCMD,"-S",server,"-d",db,"-U",user,"-P",pwd,"-C","-I","-b","-i","/tmp/fix_week_month_views.sql"],
            stderr=subprocess.STDOUT, text=True, timeout=180,
        )
        # keep last smoke lines
        tail="\n".join(out.strip().splitlines()[-8:])
        print("OK", name, db)
        print(tail)
        ok+=1
    except subprocess.CalledProcessError as e:
        fail+=1
        print("FAIL", name, (e.output or "")[-800:])
    except Exception as e:
        fail+=1
        print("ERR", name, e)

# verify najaf week count
try:
    cfg=json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8"))
    server,db,user,pwd=parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
    out=subprocess.check_output(
        [SQLCMD,"-S",server,"-d",db,"-U",user,"-P",pwd,"-C","-W","-Q",
         "SET NOCOUNT ON; SELECT COUNT(*) AS WeekCnt FROM dbo.View_CustomerWeekPaymentDevice; SELECT COUNT(*) AS MonthCnt FROM dbo.View_CustomerMonthPaymentDevice; EXEC dbo.Customers_GetWeekReceipt @DelegateID=NULL,@ShowType=N'الجميع';"],
        stderr=subprocess.STDOUT, text=True, timeout=120,
    )
    print("VERIFY_NAJAF")
    print(out[-1500:])
except Exception as e:
    print("VERIFY_ERR", e)

print("SUMMARY", ok, fail)
print("DONE")
'''.replace("__ADMINS__", repr(ADMINS))

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/fix_week_month_views.sql", "w") as f:
                f.write(sql_text)
            with sftp.file("/tmp/deploy_fix_week_month_views.py", "w") as f:
                f.write(remote)
        _, o, e = c.exec_command("python3 /tmp/deploy_fix_week_month_views.py", timeout=900)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
