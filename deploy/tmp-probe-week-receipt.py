#!/usr/bin/env python3
"""Probe week/month receipt SP + live API on Najaf."""
from __future__ import annotations

import json
import os
import urllib.error
import urllib.parse
import urllib.request

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r'''#!/usr/bin/env python3
import json, subprocess, urllib.error, urllib.parse, urllib.request
from pathlib import Path

SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"

def parse_cs(cs):
    parts = {}
    for p in cs.split(";"):
        p = p.strip()
        if not p or "=" not in p:
            continue
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("database") or parts.get("initial catalog"),
        parts.get("user id") or parts.get("uid") or parts.get("user"),
        parts.get("password") or parts.get("pwd"),
    )

cfg = json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8"))
server, db, user, pwd = parse_cs(cfg["ConnectionStrings"]["DataBaseConnection"])
print("DB", db, "server", server)

sql = """
SET NOCOUNT ON;
SELECT TOP 15 DelegateID, DelegateName FROM dbo.Delegates ORDER BY DelegateID;
SELECT COUNT(*) AS ViewCount FROM dbo.View_CustomerWeekPaymentDevice;
SELECT COUNT(*) AS ViewFake0 FROM dbo.View_CustomerWeekPaymentDevice WHERE ISNULL(IsFakeSale,0)=0;
SELECT TOP 5 DelegateID, COUNT(*) C FROM dbo.View_CustomerWeekPaymentDevice WHERE ISNULL(IsFakeSale,0)=0 GROUP BY DelegateID ORDER BY C DESC;
-- Algeria-like name
SELECT DelegateID, DelegateName FROM dbo.Delegates WHERE DelegateName LIKE N'%جزائر%' OR DelegateName LIKE N'%الجزائر%';
DECLARE @d INT = (SELECT TOP 1 DelegateID FROM dbo.Delegates WHERE DelegateName LIKE N'%جزائر%' OR DelegateName LIKE N'%الجزائر%');
PRINT CONCAT('Delegate=', ISNULL(CAST(@d AS NVARCHAR(20)),'NULL'));
EXEC dbo.Customers_GetWeekReceipt @DelegateID=@d, @ShowType=N'الجميع';
-- also null
SELECT COUNT(*) AS AllWeek FROM (
  SELECT CustomerID FROM dbo.View_CustomerWeekPaymentDevice WHERE ISNULL(IsFakeSale,0)=0
) x;
"""
out = subprocess.check_output(
    [SQLCMD, "-S", server, "-d", db, "-U", user, "-P", pwd, "-C", "-W", "-y", "0", "-Y", "0", "-Q", sql],
    stderr=subprocess.STDOUT, text=True, timeout=120,
)
print(out[-4000:])

# find najaf listen port from unit
unit = "qalaa-company-sharenewnajaf"
env = subprocess.check_output(["systemctl", "show", unit, "-p", "Environment", "--no-pager"], text=True)
port = None
for token in env.replace("Environment=", "").split():
    if token.startswith("ASPNETCORE_URLS=") or "urls" in token.lower():
        print("ENV_URL", token[:120])
# try common ports from appsettings
urls = cfg.get("Urls") or cfg.get("Kestrel")
print("cfg keys", list(cfg.keys())[:20])
# probe via localhost from nginx upstream if known
print("UNIT", subprocess.getoutput(f"systemctl show {unit} -p FragmentPath --value"))
# try curl local through known path
for base in ["http://127.0.0.1:5055", "http://127.0.0.1:5000", "http://127.0.0.1:8080"]:
    try:
        urllib.request.urlopen(base + "/swagger/index.html", timeout=2)
        print("UP", base)
    except Exception as e:
        pass

# extract ExecStart
print(subprocess.getoutput(f"systemctl cat {unit} | head -40"))
'''


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/probe_week_receipt.py", "w") as f:
                f.write(REMOTE)
        _, o, e = c.exec_command("python3 /tmp/probe_week_receipt.py", timeout=180)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
