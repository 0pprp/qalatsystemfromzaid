#!/usr/bin/env python3
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        cmd = r"""
set -e
echo ADMIN_DIRS
ls -d /opt/qalaa-runtime/Admin* 2>/dev/null | head -40
echo ---
echo DLL_SAMPLES
ls -la /opt/qalaa-runtime/AdminNajaf/BE_Company.dll 2>/dev/null || true
ls -la /opt/qalaa-runtime/AdminBasra/BE_Company.dll 2>/dev/null || true
echo ---
echo MARKERS
for d in /opt/qalaa-runtime/AdminNajaf /opt/qalaa-runtime/AdminBasra /opt/qalaa-runtime/AdminKarbala; do
  if [ -f "$d/BE_Company.dll" ]; then
    echo -n "$d "
    # BadCount is ASCII in IL metadata
    if grep -a -q BadCount "$d/BE_Company.dll"; then echo HAS_BadCount; else echo NO_BadCount; fi
  fi
done
echo ---
echo FE_MARKERS
if grep -a -q $'\xd9\x85\xd8\xaa\xd9\x84\xd9\x83\xd8\xa6' /opt/qalaa-runtime/AdminFrontend/assets/director-ratings*.js 2>/dev/null; then
  echo FE_HAS_MUTALAKI
else
  echo FE_NO_MUTALAKI
  ls /opt/qalaa-runtime/AdminFrontend/assets/director-ratings*.js 2>/dev/null | head -3
fi
"""
        i, o, e = c.exec_command(cmd, timeout=60)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[:800])
        return o.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == "__main__":
    raise SystemExit(main())
