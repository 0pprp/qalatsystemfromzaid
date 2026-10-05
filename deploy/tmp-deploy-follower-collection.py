#!/usr/bin/env python3
"""Deploy follower-collection: SQL + BE_Company + BE_DelegateWeb + FE. No Flutter APKs."""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
BE = ROOT / "BE_Company"
DELEGATE = ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication"
FE = ROOT / "FE_Company"
SQL = ROOT / "DatabaseScripts" / "Migrations" / "20261002_Delegates_FollowerCollectionEnabled.sql"
PUB_BE = ROOT / "_publish_be_follower_collection"
PUB_DEL = ROOT / "_publish_del_follower_collection"
FE_DIST = FE / "dist"
SKIP = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}
ADMINS = [
    "AdminBaghdadKarkh", "AdminBaghdadRusafa", "AdminBasra", "AdminBasraAlanwar",
    "AdminDiwaniyah", "AdminDiyala", "AdminHilla", "AdminKarakAqeel", "AdminKarbala",
    "AdminKirkuk", "AdminKot", "AdminMaysan", "AdminMosul", "AdminMuthanna",
    "AdminNajaf", "AdminNasiriyah", "AdminRusafaAqeel",
]
SERVICES = [
    "qalaa-company-sharenewdewania", "qalaa-company-sharenewkarbala", "qalaa-company-sharenewkot",
    "qalaa-company-sharenewmaysanl", "qalaa-company-sharenewmothana", "qalaa-company-sharenewmusol",
    "qalaa-company-sharenewnajaf", "qalaa-company-sharenewnasria", "qalaa-company-sharenewrbabil",
    "qalaa-company-sharenewrbasra", "qalaa-company-sharenewrbasranwar", "qalaa-company-sharenewrdeiala",
    "qalaa-company-sharenewrkarak", "qalaa-company-sharenewrkarkok", "qalaa-company-sharenewrosafa",
    "qalaa-company-shortnewkarakaqeel", "qalaa-company-shortnewrosafaaqeel",
]
BE_MARKER = b"FollowerCollectionLists"
DEL_MARKER = b"FollowerCollectionPaymentGate"


def publish(csproj: Path, out: Path) -> None:
    if out.exists():
        shutil.rmtree(out)
    r = subprocess.run(
        ["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(out),
         "--nologo", "--disable-build-servers"],
        capture_output=True, text=True,
    )
    if r.returncode:
        print(r.stdout[-2500:], r.stderr[-2500:])
        raise SystemExit(r.returncode)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not SQL.is_file():
        print("SQL missing", SQL)
        return 3

    print("publish BE_Company...", flush=True)
    publish(BE / "BE_Company.csproj", PUB_BE)
    be_dll = (PUB_BE / "BE_Company.dll").read_bytes()
    if BE_MARKER not in be_dll:
        print("BE marker missing")
        return 4
    print("BE ok", flush=True)

    print("publish BE_Delegate...", flush=True)
    publish(DELEGATE / "BE_DelegateWebApplication.csproj", PUB_DEL)
    del_dll = (PUB_DEL / "BE_DelegateWebApplication.dll").read_bytes()
    if DEL_MARKER not in del_dll:
        print("Delegate marker missing")
        return 5
    print("DEL ok", flush=True)

    print("npm build FE...", flush=True)
    r2 = subprocess.run(["npm", "run", "build"], cwd=str(FE), capture_output=True, text=True, shell=True)
    if r2.returncode:
        print(r2.stdout[-2500:], r2.stderr[-2500:])
        return r2.returncode
    print("FE ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    be_tar = ROOT / f"_be_fc_{stamp}.tgz"
    del_tar = ROOT / f"_del_fc_{stamp}.tgz"
    fe_tar = ROOT / f"_fe_fc_{stamp}.tgz"
    sql_remote = f"/tmp/fc_sql_{stamp}.sql"

    with tarfile.open(be_tar, "w:gz") as tar:
        for p in PUB_BE.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_BE)).replace("\\", "/"))
    with tarfile.open(del_tar, "w:gz") as tar:
        for p in PUB_DEL.rglob("*"):
            if p.is_file() and p.name not in SKIP:
                tar.add(p, arcname=str(p.relative_to(PUB_DEL)).replace("\\", "/"))
    with tarfile.open(fe_tar, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))

    remote = r'''
import hashlib, json, os, shutil, subprocess, tarfile, time
from pathlib import Path

stamp = __STAMP__
skip = set(__SKIP__)
admins = __ADMINS__
services = __SERVICES__
be_marker = __BE_MARKER__
del_marker = __DEL_MARKER__
sql_path = __SQL_PATH__
SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"

bx = Path("/tmp/be_fc_x_" + stamp)
dx = Path("/tmp/del_fc_x_" + stamp)
fx = Path("/tmp/fe_fc_x_" + stamp)
for x in (bx, dx, fx):
    if x.exists():
        shutil.rmtree(x)
    x.mkdir()
tarfile.open("/tmp/be_fc_" + stamp + ".tgz").extractall(bx)
tarfile.open("/tmp/del_fc_" + stamp + ".tgz").extractall(dx)
tarfile.open("/tmp/fe_fc_" + stamp + ".tgz").extractall(fx)

be = (bx / "BE_Company.dll").read_bytes()
de = (dx / "BE_DelegateWebApplication.dll").read_bytes()
print("BE", len(be), hashlib.sha256(be).hexdigest()[:16], "mark", be_marker in be)
print("DEL", len(de), hashlib.sha256(de).hexdigest()[:16], "mark", del_marker in de)
if be_marker not in be or del_marker not in de:
    raise SystemExit("marker missing")

def parse_cs(cs: str):
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

sql_ok = 0
for name in admins:
    cfg = Path("/opt/qalaa-runtime") / name / "appsettings.json"
    if not cfg.is_file():
        print("SQL_SKIP", name); continue
    try:
        data = json.loads(cfg.read_text(encoding="utf-8-sig"))
        cs = (data.get("ConnectionStrings") or {}).get("DataBaseConnection") or ""
        server, db, user, pwd = parse_cs(cs)
        if not all([server, db, user, pwd]):
            print("SQL_BAD_CS", name); continue
        r = subprocess.run(
            [SQLCMD, "-C", "-I", "-S", server, "-d", db, "-U", user, "-P", pwd, "-i", sql_path, "-b"],
            capture_output=True, text=True,
        )
        if r.returncode:
            print("SQL_FAIL", name, (r.stdout or "")[-400], (r.stderr or "")[-400])
        else:
            sql_ok += 1
            print("SQL_OK", name, db)
    except Exception as e:
        print("SQL_ERR", name, e)
print("SQL", sql_ok, "/", len(admins))

for svc in services:
    subprocess.run(["systemctl", "stop", svc], check=False)
time.sleep(2)
n = 0
for name in admins:
    dest = Path("/opt/qalaa-runtime") / name
    if not dest.is_dir():
        print("SKIP", name); continue
    for root, dirs, files in os.walk(bx):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(bx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    n += 1
    print("BE", name, "ok")
demo = Path("/opt/qalaa-demo/backend")
if demo.is_dir():
    for root, dirs, files in os.walk(bx):
        for f in files:
            if f in skip:
                continue
            src = Path(root) / f
            rel = src.relative_to(bx)
            dst = demo / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    print("BE demo")
for svc in services:
    subprocess.run(["systemctl", "start", svc], check=False)

dirs = []
for name in sorted(os.listdir("/opt/qalaa-runtime")):
    if not name.lower().startswith("delegate"):
        continue
    if "backup" in name.lower() or name.endswith(".bak"):
        continue
    d = os.path.join("/opt/qalaa-runtime", name)
    if os.path.isfile(os.path.join(d, "BE_DelegateWebApplication.dll")):
        dirs.append(d)
demo_f = "/opt/qalaa-demo/follower-backend"
if os.path.isfile(os.path.join(demo_f, "BE_DelegateWebApplication.dll")):
    dirs.append(demo_f)
print("DEL_DIRS", len(dirs))
for dest in dirs:
    for root, dirs_, files in os.walk(dx):
        for f in files:
            if f in skip:
                continue
            full = os.path.join(root, f)
            rel = os.path.relpath(full, dx)
            target = os.path.join(dest, rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(full, target)
    print("DEL", os.path.basename(dest))

units = []
for d in dirs:
    cmd = (
        "for u in $(systemctl list-units --type=service --all --no-legend 'qalaa-delegate*' | awk '{print $1}'); do "
        'wd=$(systemctl show "$u" -p WorkingDirectory --value 2>/dev/null || true); '
        f'if [ "$wd" = "{d}" ]; then echo "$u"; fi; '
        "done"
    )
    p = subprocess.run(["bash", "-lc", cmd], capture_output=True, text=True)
    for line in (p.stdout or "").splitlines():
        u = line.strip()
        if u and u not in units:
            units.append(u)
for u in ["qalaa-demo-follower.service"]:
    if subprocess.call(["systemctl", "cat", u], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0:
        if u not in units:
            units.append(u)
print("DEL_RESTART", len(units))
for u in units:
    subprocess.call(["systemctl", "restart", u])

fe_ok = False
for dest_s in ["/opt/qalaa-runtime/AdminFrontend", "/opt/qalaa-demo/frontend"]:
    dest = Path(dest_s)
    if not dest.is_dir():
        continue
    for root, dirs_, files in os.walk(fx):
        for f in files:
            src = Path(root) / f
            rel = src.relative_to(fx)
            dst = dest / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    touch = dest / "index.html"
    if touch.is_file():
        touch.touch()
    hit = False
    needle = "جباية من قبل المتابعين".encode("utf-8")
    for p in dest.rglob("*.js"):
        try:
            raw = p.read_bytes()
            if b"follower-collection-lists" in raw or needle in raw:
                hit = True
                break
        except Exception:
            pass
    print("FE", dest_s, "page", hit)
    fe_ok = fe_ok or hit
subprocess.run(["systemctl", "reload", "nginx"], check=False)
print("OK company", n, "sql", sql_ok, "del", len(dirs), "fe", fe_ok)
print("DONE")
'''

    remote = (
        remote.replace("__STAMP__", repr(stamp))
        .replace("__SKIP__", repr(sorted(SKIP)))
        .replace("__ADMINS__", repr(ADMINS))
        .replace("__SERVICES__", repr(SERVICES))
        .replace("__BE_MARKER__", repr(BE_MARKER))
        .replace("__DEL_MARKER__", repr(DEL_MARKER))
        .replace("__SQL_PATH__", repr(sql_remote))
    )
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    print("ssh...", flush=True)
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(be_tar), f"/tmp/be_fc_{stamp}.tgz")
        sftp.put(str(del_tar), f"/tmp/del_fc_{stamp}.tgz")
        sftp.put(str(fe_tar), f"/tmp/fe_fc_{stamp}.tgz")
        sftp.put(str(SQL), sql_remote)
        with sftp.file(f"/tmp/deploy_fc_{stamp}.py", "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 /tmp/deploy_fc_{stamp}.py", timeout=1200)
        print(stdout.read().decode("utf-8", "replace"))
        err = stderr.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:2000])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
