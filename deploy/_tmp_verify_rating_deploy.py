#!/usr/bin/env python3
import os
import sys

import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=40,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
stat -c %y /opt/qalaa-runtime/AdminFrontend/index.html
stat -c %y /opt/qalaa-runtime/AdminNajaf/BE_Company.dll
ls /opt/qalaa-runtime/AdminFrontend/assets | wc -l
grep -l "day-count\|نقطة يومية\|زبائن الفترة" /opt/qalaa-runtime/AdminFrontend/assets/*.js 2>/dev/null | head -3
strings /opt/qalaa-runtime/AdminNajaf/BE_Company.dll 2>/dev/null | grep -F "متوسط نقاط زبائن الفترة" | head -1
echo VERIFY_DONE
"""
_, o, e = c.exec_command(cmd, timeout=60)
print(o.read().decode("utf-8", "replace"))
err = e.read().decode("utf-8", "replace")
if err.strip():
    print("ERR", err[:500])
c.close()
