#!/usr/bin/env python3
"""Publish + deploy BE_Delegate followup/media fix to all Delegate* runtimes."""
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
PROJ = ROOT / "BE_DelegateWebApplication" / "BE_DelegateWebApplication"
PUBLISH = ROOT / "_publish_delegate_followup_media"
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

    if PUBLISH.exists():
        shutil.rmtree(PUBLISH)
    PUBLISH.mkdir(parents=True)
    print("dotnet publish...", flush=True)
    r = subprocess.run(
        [
            "dotnet",
            "publish",
            str(PROJ / "BE_DelegateWebApplication.csproj"),
            "-c",
            "Release",
            "-o",
            str(PUBLISH),
            "--nologo",
        ],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stdout[-2000:])
        print(r.stderr[-3000:])
        return r.returncode
    print("publish ok", flush=True)

    stamp = time.strftime("%Y%m%d_%H%M%S")
    remote_tar = f"/opt/qalaa-updates/delegate-followup-media-{stamp}.tar.gz"
    local_tar = ROOT / f"_publish_delegate_followup_media_{stamp}.tar.gz"
    if local_tar.exists():
        local_tar.unlink()
    with tarfile.open(local_tar, "w:gz") as tar:
        for root, _dirs, files in os.walk(PUBLISH):
            for name in files:
                if name.lower() in {x.lower() for x in SKIP_FILES}:
                    continue
                full = Path(root) / name
                tar.add(full, arcname=full.relative_to(PUBLISH).as_posix())
    print(f"Tar MB={local_tar.stat().st_size / (1024*1024):.1f}", flush=True)

    c = connect()
    try:
        code, out, err = run(
            c,
            "systemctl list-units --type=service --all --no-legend 'qalaa-delegate*' | awk '{print $1}'",
        )
        units = [u.strip().removesuffix(".service") for u in out.splitlines() if u.strip()]
        print("UNITS", len(units), flush=True)

        code2, out2, _ = run(
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

        sftp = c.open_sftp()
        sftp.put(str(local_tar), remote_tar)
        sftp.close()

        for d in runtime_dirs:
            dest = f"/opt/qalaa-runtime/{d}"
            bak = f"/opt/qalaa-backups/{d}-pre-followup-media-{stamp}"
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

        # SalesDocumentsRoot drop-ins
        run(
            c,
            r"""python3 <<'PY'
from pathlib import Path
pairs = {
  'qalaa-delegatehaidernajaflast.service': '/opt/qalaa-runtime/AdminNajaf/App_Data',
  'qalaa-delegatehaiderbasralast.service': '/opt/qalaa-runtime/AdminBasra/App_Data',
  'qalaa-delegatehaideralanwarlast.service': '/opt/qalaa-runtime/AdminBasraAlanwar/App_Data',
  'qalaa-delegatehaiderdiyalalast.service': '/opt/qalaa-runtime/AdminDiyala/App_Data',
  'qalaa-delegatehaideralmuthannalast.service': '/opt/qalaa-runtime/AdminMuthanna/App_Data',
  'qalaa-delegatehaideralqadisiyyahlast.service': '/opt/qalaa-runtime/AdminDiwaniyah/App_Data',
  'qalaa-delegatehaiderbabillast.service': '/opt/qalaa-runtime/AdminHilla/App_Data',
  'qalaa-delegatehaiderkarbalalast.service': '/opt/qalaa-runtime/AdminKarbala/App_Data',
  'qalaa-delegatehaiderkirkuklast.service': '/opt/qalaa-runtime/AdminKirkuk/App_Data',
  'qalaa-delegatehaiderdhiqarlast.service': '/opt/qalaa-runtime/AdminNasiriyah/App_Data',
  'qalaa-delegatehaiderninevehlast.service': '/opt/qalaa-runtime/AdminMosul/App_Data',
  'qalaa-delegatehaiderkaraklast.service': '/opt/qalaa-runtime/AdminBaghdadKarkh/App_Data',
  'qalaa-delegate-haider-rusafa.service': '/opt/qalaa-runtime/AdminBaghdadRusafa/App_Data',
  'qalaa-delegatehaiderrusafalast.service': '/opt/qalaa-runtime/AdminBaghdadRusafa/App_Data',
  'qalaa-delegateaqeelkaraklast.service': '/opt/qalaa-runtime/AdminKarakAqeel/App_Data',
  'qalaa-delegateaqeelrusafalast.service': '/opt/qalaa-runtime/AdminRusafaAqeel/App_Data',
  'qalaa-delegateaqeelwasitlast.service': '/opt/qalaa-runtime/AdminKot/App_Data',
  'qalaa-delegateaqeelmaysanlast.service': '/opt/qalaa-runtime/AdminMaysan/App_Data',
}
for svc, root in pairs.items():
  unit = Path('/etc/systemd/system')/svc
  if not unit.exists():
    print('skip', svc); continue
  drop = Path('/etc/systemd/system')/(svc + '.d')
  drop.mkdir(parents=True, exist_ok=True)
  (drop/'sales-docs.conf').write_text('[Service]\nEnvironment=SalesDocumentsRoot='+root+'\n')
  print('env', svc, '->', root)
PY""",
            timeout=60,
        )
        run(c, "systemctl daemon-reload", timeout=60)

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
            "print('record-complete', b'record-complete' in b)\n"
            "print('TryPairedCompanyAppData', b'TryPairedCompanyAppData' in b or b'PairedAdminFolderNames' in b)\n"
            "print('SalesDocumentsRoot marker via env drop-in ok')\n"
            "PY",
        )
        print(out)
        if "record-complete True" not in out:
            print("DLL marker missing")
            return 1
        print("OK followup-media deploy")
        return 0
    finally:
        c.close()
        try:
            local_tar.unlink(missing_ok=True)
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
