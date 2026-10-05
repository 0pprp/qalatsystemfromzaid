import hashlib
import json
import os
import shutil
import subprocess
import tarfile
from pathlib import Path

stamp = os.environ["STAMP"]
skip = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
admins = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
marker = "ExceptionHoldStatus IS NOT NULL".encode("utf-16le")

extract = Path("/tmp/be_hold_null_x_" + stamp)
if extract.exists():
    shutil.rmtree(extract)
extract.mkdir()
with tarfile.open("/tmp/be_hold_null_" + stamp + ".tgz") as t:
    t.extractall(extract)
data = (extract / "BE_Company.dll").read_bytes()
print("TAR", len(data), hashlib.sha256(data).hexdigest()[:16], marker in data)
if marker not in data:
    raise SystemExit("marker missing")

units_out = subprocess.getoutput("systemctl list-units --type=service --all --no-legend")
wd_to_unit = {}
for line in units_out.splitlines():
    u = line.split()[0] if line.strip() else ""
    if not u.endswith(".service"):
        continue
    if "qalaa-company" not in u and "demo-backend" not in u:
        continue
    wd = subprocess.getoutput(f"systemctl show {u} -p WorkingDirectory --value").strip()
    if wd:
        wd_to_unit[wd] = u

targets = []
for name in admins:
    d = Path("/opt/qalaa-runtime") / name
    if (d / "BE_Company.dll").is_file():
        targets.append(d)
for d in Path("/opt/qalaa-runtime").glob("Admin*"):
    if (d / "BE_Company.dll").is_file() and d not in targets:
        if "backup" in d.name.lower() or d.name.endswith("-old") or ".bak" in d.name.lower():
            continue
        targets.append(d)
demo = Path("/opt/qalaa-demo/backend")
if (demo / "BE_Company.dll").is_file():
    targets.append(demo)

restarted = set()
for dest in targets:
    for root, dirs, files in os.walk(extract):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(extract)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print("BE", dest)
    unit = wd_to_unit.get(str(dest))
    if unit and unit not in restarted:
        subprocess.run(["systemctl", "restart", unit], check=False)
        restarted.add(unit)
        print("RESTART", unit)

najaf_unit = "qalaa-company-sharenewnajaf.service"
drop = Path("/etc/systemd/system/qalaa-company-sharenewnajaf.service.d")
drop.mkdir(parents=True, exist_ok=True)
conf = drop / "branch-id.conf"
conf.write_text(
    "[Service]\n"
    "Environment=SalesManagement__BranchId=1\n"
    "Environment=SalesManagement__BranchName=النجف\n",
    encoding="utf-8",
)
subprocess.run(["systemctl", "daemon-reload"], check=False)
subprocess.run(["systemctl", "restart", najaf_unit], check=False)
print("NAJAF_BRANCHID_FILE", conf.read_text(encoding="utf-8"))
env = subprocess.getoutput(f"systemctl show {najaf_unit} -p Environment --value")
print("NAJAF_HAS_BRANCHID", "SalesManagement__BranchId=1" in env)

cfg = json.loads(Path("/opt/qalaa-runtime/AdminNajaf/appsettings.json").read_text(encoding="utf-8", errors="replace"))
cs = (cfg.get("ConnectionStrings") or {}).get("DataBaseConnection")
parts = {}
for part in cs.split(";"):
    if "=" in part:
        k, v = part.split("=", 1)
        parts[k.strip().lower()] = v.strip()
server = parts.get("server") or parts.get("data source")
db = parts.get("initial catalog") or parts.get("database")
user = parts.get("user id") or parts.get("uid")
pwd = parts.get("password") or parts.get("pwd")
sql = """SET NOCOUNT ON;
UPDATE dbo.SalesRequests
SET CityValue = N'1', CityName = N'النجف', CustomerProvince = CASE
  WHEN CustomerProvince IS NULL OR CustomerProvince = N'' OR CustomerProvince = N'DatabaseCompanyNajaf'
  THEN N'النجف' ELSE CustomerProvince END
WHERE Id = 202 AND CreatedByName = N'test12';
SELECT Id, Status, CustomerName, CustomerPhone, CustomerSourceType, CityValue, CityName, CreatedByName, TargetEmployeeId, ExceptionHoldStatus
FROM dbo.SalesRequests WHERE Id = 202;

PRINT 'sent fixed count';
SELECT COUNT(1) AS SentFixed
FROM dbo.SalesRequests R
WHERE ISNULL(R.IsDeleted,0)=0 AND R.Status=N'New'
  AND R.CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate')
  AND NOT (
    R.ExceptionHoldStatus IS NOT NULL
    AND R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
    AND ISNULL(R.TargetEmployeeId, 0) <= 0
  );
"""
Path("/tmp/fix_req202.sql").write_text(sql, encoding="utf-8")
r = subprocess.run(
    ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", server, "-d", db, "-U", user, "-P", pwd, "-i", "/tmp/fix_req202.sql", "-s", "|", "-w", "65535"],
    capture_output=True,
    text=True,
)
print(r.stdout)
print("SQLRC", r.returncode)
print("DONE")
