#!/usr/bin/env python3
"""Deploy FE only — accountant ratings nav visibility + dashboard CTA + no-cache index."""
from __future__ import annotations

import os
import subprocess
import sys
import tarfile
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
FE = ROOT / "FE_Company"
FE_DIST = FE / "dist"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    print("npm build FE...", flush=True)
    r = subprocess.run(
        ["npm", "run", "build"],
        cwd=str(FE),
        capture_output=True,
        text=True,
        shell=True,
    )
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-3000:])
        return r.returncode
    if not any(FE_DIST.rglob("accountant-ratings*.js")):
        print("FATAL: accountant-ratings missing")
        return 3
    print("FE build ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    tar_path = ROOT / f"_fe_acct_nav_{stamp}.tgz"
    if tar_path.exists():
        tar_path.unlink()
    with tarfile.open(tar_path, "w:gz") as tar:
        for p in FE_DIST.rglob("*"):
            if p.is_file():
                tar.add(p, arcname=str(p.relative_to(FE_DIST)).replace("\\", "/"))

    remote = f'''
set -e
TGZ=/tmp/fe_acct_nav_{stamp}.tgz
rm -rf /tmp/fe_acct_nav_x_{stamp}
mkdir -p /tmp/fe_acct_nav_x_{stamp}
tar -xzf "$TGZ" -C /tmp/fe_acct_nav_x_{stamp}
for dest in /opt/qalaa-runtime/AdminFrontend /opt/qalaa-demo/frontend; do
  if [ -d "$dest" ]; then
    rsync -a /tmp/fe_acct_nav_x_{stamp}/ "$dest/"
    # bust browser cache for SPA entry
    touch "$dest/index.html"
    printf '%s\\n' 'Cache-Control: no-store, no-cache, must-revalidate' > "$dest/index.html.headers" 2>/dev/null || true
    echo FE "$dest"
    grep -l "accountant-ratings" "$dest"/assets/DefaultLayoutWithVerticalNav-*.js | head -1 || true
    ls -1 "$dest"/assets/accountant-ratings-*.js | head -1 || true
  fi
done
# nginx: ensure index.html is not cached long
if [ -d /etc/nginx ]; then
  cat > /etc/nginx/conf.d/00-adminfrontend-nocache-index.conf <<'NGX'
# SPA entry must not stick to old hashed chunks
map $uri $admin_index_nocache {{
  default "";
  /index.html "1";
  / "1";
}}
NGX
  # Prefer location snippet if site uses AdminFrontend root — best-effort reload only
  systemctl reload nginx 2>/dev/null || true
fi
echo FE_OK
'''

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        print("upload...", flush=True)
        sftp.put(str(tar_path), f"/tmp/fe_acct_nav_{stamp}.tgz")
        sftp.close()
        _, out, err = c.exec_command(remote, timeout=300)
        print(out.read().decode("utf-8", "replace"))
        e = err.read().decode("utf-8", "replace")
        if e.strip():
            print("STDERR", e[:1200])
        return out.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
