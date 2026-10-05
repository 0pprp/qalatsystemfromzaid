#!/usr/bin/env python3
"""Deploy BE_Delegate dedupe fix + apply GetDelegateSelect SP on all Delegate branch DBs."""
from __future__ import annotations

import os
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUBLISH = ROOT / "_publish_delegate_field"
SQL_FILE = ROOT / "DatabaseScripts" / "Deploy" / "DelegateStoppedList_Dedupe_Deploy.sql"
SKIP = {"appsettings.json", "appsettings.Development.json", "appsettings.Production.json"}


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not PUBLISH.is_dir():
        print("missing publish", PUBLISH)
        return 2
    sql = SQL_FILE.read_text(encoding="utf-8")

    stamp = time.strftime("%Y%m%d_%H%M%S")
    local_tar = ROOT / f"_publish_delegate_dedupe_{stamp}.tar.gz"
    remote_tar = f"/opt/qalaa-updates/delegate-dedupe-{stamp}.tar.gz"

    with tarfile.open(local_tar, "w:gz") as tar:
        for root, _dirs, files in os.walk(PUBLISH):
            for name in files:
                if name.lower() in {x.lower() for x in SKIP}:
                    continue
                full = Path(root) / name
                tar.add(full, arcname=full.relative_to(PUBLISH).as_posix())
    print("tar", local_tar.stat().st_size)

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        with sftp.file("/tmp/delegate_dedupe.sql", "w") as f:
            f.write(sql)
        sftp.close()

        _, out, _ = c.exec_command(
            "systemctl list-units --type=service --all --no-legend 'qalaa-delegate*' | awk '{print $1}'",
            timeout=60,
        )
        units = [u.strip().removesuffix(".service") for u in out.read().decode().splitlines() if u.strip()]
        print("UNITS", len(units))

        _, out2, _ = c.exec_command(
            "python3 - <<'PY'\n"
            "import os\n"
            "root='/opt/qalaa-runtime'\n"
            "for name in sorted(os.listdir(root)):\n"
            "  p=os.path.join(root,name)\n"
            "  if name.lower().startswith('delegate') and os.path.isdir(p) and 'backup' not in name.lower():\n"
            "    print(name)\n"
            "PY",
            timeout=60,
        )
        dirs = [x.strip() for x in out2.read().decode().splitlines() if x.strip()]
        print("DIRS", len(dirs))

        for d in dirs:
            dest = f"/opt/qalaa-runtime/{d}"
            cmd = f"""bash -lc 'set -e
tmpdir=$(mktemp -d)
tar -xzf "{remote_tar}" -C "$tmpdir"
rsync -a --exclude "appsettings.json" --exclude "appsettings.*.json" --exclude "wwwroot/Images" --exclude "Logs" "$tmpdir/" "{dest}/"
chown -R qalaa:qalaa "{dest}" 2>/dev/null || true
rm -rf "$tmpdir"
'"""
            _, o, e = c.exec_command(cmd, timeout=300)
            code = o.channel.recv_exit_status()
            print("Deploy", d, code)
            if code != 0:
                print(e.read().decode()[:300])
                return 1

        for u in units:
            c.exec_command(f"systemctl restart {u}.service", timeout=120)
            time.sleep(0.05)
        time.sleep(4)

        # Apply SP on each Delegate appsettings DB
        remote_py = r'''
import json, pathlib, subprocess, re
sql_path="/tmp/delegate_dedupe.sql"
ok=0; fail=0; seen=set()
for apps in sorted(pathlib.Path("/opt/qalaa-runtime").glob("Delegate*/appsettings.json")):
    name=apps.parent.name
    if "backup" in name.lower(): continue
    try:
        cfg=json.loads(apps.read_text(encoding="utf-8", errors="replace"))
    except Exception as ex:
        print("SKIP", name, ex); fail+=1; continue
    cs=(cfg.get("ConnectionStrings") or {}).get("DataBaseConnection") or ""
    parts={}
    for part in cs.split(";"):
        if "=" in part:
            k,v=part.split("=",1); parts[k.strip().lower()]=v.strip()
    server=parts.get("server") or parts.get("data source")
    db=parts.get("initial catalog") or parts.get("database")
    user=parts.get("user id") or parts.get("uid")
    pwd=parts.get("password") or parts.get("pwd")
    if not all([server, db, user, pwd]):
        print("BAD_CS", name); fail+=1; continue
    key=(server, db)
    if key in seen:
        print("SKIP_DUP_DB", name, db); continue
    seen.add(key)
    r=subprocess.run(["/opt/mssql-tools18/bin/sqlcmd","-C","-S",server,"-d",db,"-U",user,"-P",pwd,"-i",sql_path,"-b"],capture_output=True,text=True)
    print("SQL", name, db, "RC", r.returncode)
    if r.returncode!=0:
        print((r.stdout or "")[:200], (r.stderr or "")[:200]); fail+=1
    else:
        ok+=1
print("DONE ok", ok, "fail", fail)
'''
        with c.open_sftp() as sftp2:
            with sftp2.file("/tmp/apply_dedupe.py", "w") as f:
                f.write(remote_py)
        _, o, e = c.exec_command("python3 /tmp/apply_dedupe.py", timeout=600)
        sys.stdout.buffer.write(o.read())
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:400])
        code = o.channel.recv_exit_status()

        _, o3, _ = c.exec_command(
            "python3 - <<'PY'\n"
            "from pathlib import Path\n"
            "b=Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll').read_bytes()\n"
            "print('DelegateListDedupe', b'DelegateListDedupe' in b)\n"
            "PY",
            timeout=30,
        )
        print(o3.read().decode())
        return 0 if code == 0 else code
    finally:
        c.close()
        try:
            local_tar.unlink(missing_ok=True)
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
