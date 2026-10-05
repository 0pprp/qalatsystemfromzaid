#!/usr/bin/env python3
from __future__ import annotations
import os, paramiko
HOST="169.58.236.52"
SSH_PASS=os.environ["SSH_PASS"]
REMOTE=r"""
ls -la /opt/qalaa-runtime/AdminNajaf/BE_Company.dll
stat /opt/qalaa-runtime/AdminNajaf/BE_Company.dll | head -8
echo '--- occurrences ---'
strings /opt/qalaa-runtime/AdminNajaf/BE_Company.dll | grep -n 'تعذر ربط المبيع\|الحذف التعويضي\|إبطال المسودة وأرشفة' | head -20
echo '--- utf16 search ---'
python3 - <<'PY'
from pathlib import Path
b=Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll').read_bytes()
old='تعذر ربط المبيع الرسمي بالمسودة'.encode('utf-8')
old16='تعذر ربط المبيع الرسمي بالمسودة'.encode('utf-16le')
new='إبطال المسودة وأرشفة الطلب فقط'.encode('utf-8')
new16='إبطال المسودة وأرشفة الطلب فقط'.encode('utf-16le')
print('old utf8', b.find(old)>=0, 'old utf16', b.find(old16)>=0)
print('new utf8', b.find(new)>=0, 'new utf16', b.find(new16)>=0)
print('ShouldStopAfterPhantom', b.find(b'ShouldStopAfterPhantomStoredId')>=0)
print('dll size', len(b))
PY
systemctl is-active qalaa-company-sharenewnajaf.service
journalctl -u qalaa-company-sharenewnajaf.service --no-pager -n 15
"""
c=paramiko.SSHClient();c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username='root',password=SSH_PASS,timeout=40,allow_agent=False,look_for_keys=False)
_,o,e=c.exec_command(REMOTE,timeout=60)
print(o.read().decode('utf-8','replace'))
print(e.read().decode('utf-8','replace')[-800:])
c.close()
