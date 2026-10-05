import json
import subprocess
from pathlib import Path


def parse_cs(cs):
    parts = {}
    for part in (cs or "").split(";"):
        if "=" in part:
            k, v = part.split("=", 1)
            parts[k.strip().lower()] = v.strip()
    return (
        parts.get("server") or parts.get("data source"),
        parts.get("initial catalog") or parts.get("database"),
        parts.get("user id") or parts.get("uid"),
        parts.get("password") or parts.get("pwd"),
    )


def sqlcmd(server, db, user, pwd, sql, path):
    Path(path).write_text(sql, encoding="utf-8")
    r = subprocess.run(
        [
            "/opt/mssql-tools18/bin/sqlcmd",
            "-C",
            "-S",
            server,
            "-d",
            db,
            "-U",
            user,
            "-P",
            pwd,
            "-i",
            path,
            "-W",
            "-s",
            "|",
            "-y",
            "0",
            "-Y",
            "0",
        ],
        capture_output=True,
        text=True,
    )
    print(r.stdout)
    if r.returncode != 0:
        print("SQLERR", (r.stderr or "")[:1200])
    print("RC", r.returncode)
    return r.returncode


units = subprocess.getoutput("systemctl list-units --type=service --all --no-legend")
najaf_unit = None
najaf_wd = None
for line in units.splitlines():
    u = line.split()[0] if line.strip() else ""
    if not u.endswith(".service"):
        continue
    if "najaf" not in u.lower():
        continue
    wd = subprocess.getoutput(f"systemctl show {u} -p WorkingDirectory --value").strip()
    print("CAND", u, wd)
    if wd and "AdminNajaf" in wd and ".bak" not in wd:
        najaf_unit = u
        najaf_wd = wd

print("PICK", najaf_unit, najaf_wd)
if not najaf_unit:
    raise SystemExit("no najaf unit")

pid = subprocess.getoutput(f"systemctl show {najaf_unit} -p MainPID --value").strip()
print("PID", pid)
cs = None
if pid and pid != "0":
    env = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
    for e in env:
        s = e.decode("utf-8", "replace")
        if s.startswith("ConnectionStrings__DataBaseConnection="):
            cs = s.split("=", 1)[1]
            print("CS_FROM DataBaseConnection")
        elif s.startswith("ConnectionStrings__SalesDemoConnection=") and not cs:
            cs = s.split("=", 1)[1]
            print("CS_FROM SalesDemoConnection")

if not cs and najaf_wd:
    for name in ("appsettings.Production.json", "appsettings.json"):
        p = Path(najaf_wd) / name
        if not p.is_file():
            continue
        cfg = json.loads(p.read_text(encoding="utf-8", errors="replace"))
        cstrings = cfg.get("ConnectionStrings") or {}
        cs = cstrings.get("DataBaseConnection") or cstrings.get("SalesDemoConnection")
        if cs:
            print("CS_FROM", name)
            break

if not cs:
    print("NO_CS")
    raise SystemExit(3)

server, db, user, pwd = parse_cs(cs)
print("DB", server, db, user)

print("=== JOURNAL (filtered) ===")
j = subprocess.getoutput(f"journalctl -u {najaf_unit} --since '12 hours ago' --no-pager -n 800")
keys = ("requests/submit", "SalesRequest", "test12", "EmployeeSubmitted", "POST /api/sales")
hits = [ln for ln in j.splitlines() if any(k.lower() in ln.lower() for k in keys)]
print("\n".join(hits[-100:] if hits else ["(no journal hits)"]))

schema = """
SET NOCOUNT ON;
PRINT 'TABLES';
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE N'%SalesRequest%' OR TABLE_NAME IN (N'SalesEmployees', N'Users', N'GetAdmin')
ORDER BY TABLE_NAME;

PRINT 'SalesRequests COLS';
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = N'SalesRequests'
ORDER BY ORDINAL_POSITION;
"""
print("=== SCHEMA ===")
sqlcmd(server, db, user, pwd, schema, "/tmp/najaf_sr_schema.sql")
