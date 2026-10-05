#!/usr/bin/env python3
"""Deploy BE_Delegate with FieldFollowups to all Delegate* runtimes."""
from __future__ import annotations

import os
import sys
import time
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
ROOT = Path(__file__).resolve().parent.parent
PUBLISH = ROOT / "_publish_delegate_field"

SKIP_FILES = {
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
}


def connect():
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    t = c.get_transport()
    if t:
        t.set_keepalive(15)
    return c


def run(c, cmd, timeout=600):
    stdin, stdout, stderr = c.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode("utf-8", "replace")
    err = stderr.read().decode("utf-8", "replace")
    code = stdout.channel.recv_exit_status()
    return code, out, err


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    if not PUBLISH.is_dir():
        print("missing publish", PUBLISH)
        return 2

    stamp = time.strftime("%Y%m%d_%H%M%S")
    remote_tar = f"/opt/qalaa-updates/delegate-field-{stamp}.tar.gz"
    local_tar = ROOT / f"_publish_delegate_field_{stamp}.tar.gz"

    print("Creating tar...", flush=True)
    import tarfile

    if local_tar.exists():
        local_tar.unlink()
    with tarfile.open(local_tar, "w:gz") as tar:
        for root, _dirs, files in os.walk(PUBLISH):
            for name in files:
                if name.lower() in {x.lower() for x in SKIP_FILES}:
                    continue
                full = Path(root) / name
                arc = full.relative_to(PUBLISH).as_posix()
                tar.add(full, arcname=arc)
    print(f"Tar MB={local_tar.stat().st_size / (1024*1024):.1f}", flush=True)

    c = connect()
    try:
        code, out, err = run(
            c,
            "systemctl list-units --type=service --all --no-legend 'qalaa-delegate*' | awk '{print $1}'",
        )
        units = [u.strip().removesuffix(".service") for u in out.splitlines() if u.strip()]
        print("UNITS", len(units), flush=True)

        code2, out2, _err2 = run(
            c,
            "python3 - <<'PY'\n"
            "import os\n"
            "root='/opt/qalaa-runtime'\n"
            "for name in sorted(os.listdir(root)):\n"
            "  if name.lower().startswith('delegate') and os.path.isdir(os.path.join(root,name)):\n"
            "    if 'backup' in name.lower() or name.endswith('.bak'):\n"
            "      continue\n"
            "    print(name)\n"
            "PY",
        )
        runtime_dirs = [x.strip() for x in out2.splitlines() if x.strip()]
        print("DIRS", len(runtime_dirs), flush=True)

        print("Uploading tar...", flush=True)
        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        sftp.close()

        for d in runtime_dirs:
            dest = f"/opt/qalaa-runtime/{d}"
            bak = f"/opt/qalaa-backups/{d}-pre-field-{stamp}"
            print(f"Deploy {d}", flush=True)
            code, out, err = run(
                c,
                f"""bash -lc 'set -e
mkdir -p "{bak}"
cp -a "{dest}/BE_DelegateWebApplication.dll" "{bak}/" 2>/dev/null || true
tmpdir=$(mktemp -d)
tar -xzf "{remote_tar}" -C "$tmpdir"
rsync -a --exclude "appsettings.json" --exclude "appsettings.*.json" --exclude "wwwroot/Images" --exclude "Logs" "$tmpdir/" "{dest}/"
chown -R qalaa:qalaa "{dest}" 2>/dev/null || true
rm -rf "$tmpdir"
'""",
                timeout=300,
            )
            if code != 0:
                print("FAIL", d, err[:400], out[:200])
                return 1

        print("Restarting delegates...", flush=True)
        for u in units:
            run(c, f"systemctl restart {u}.service", timeout=120)
            time.sleep(0.1)
        time.sleep(5)
        code, out, err = run(c, "systemctl is-active " + " ".join(u + ".service" for u in units))
        print(out)
        if "inactive" in out or "failed" in out:
            print("some inactive")
            return 1

        code, out, err = run(
            c,
            "python3 - <<'PY'\n"
            "from pathlib import Path\n"
            "b=Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll').read_bytes()\n"
            "print('FieldFollowups', b'FieldFollowups' in b)\n"
            "print('FollowerFieldFollowups', b'FollowerFieldFollowups' in b)\n"
            "PY",
        )
        print(out)
        if "FieldFollowups True" not in out:
            print("DLL marker missing")
            return 1
        print("OK field-followup deploy")
        return 0
    finally:
        c.close()
        try:
            local_tar.unlink(missing_ok=True)
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
