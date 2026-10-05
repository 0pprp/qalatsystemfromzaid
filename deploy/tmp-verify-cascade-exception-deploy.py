#!/usr/bin/env python3
"""Post-deploy verify: old error gone; cascade soft-delete no longer hard-fails on phantom CustomerSaleId."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r"""
set -e
echo '=== Old hard-fail string in AdminNajaf dll? ==='
if strings /opt/qalaa-runtime/AdminNajaf/BE_Company.dll 2>/dev/null | grep -F 'تعذر ربط المبيع الرسمي بالمسودة' | head -3; then
  echo 'FOUND_OLD_STRING'
else
  echo 'OLD_STRING_ABSENT_OK'
fi
echo '=== New warning string present? ==='
strings /opt/qalaa-runtime/AdminNajaf/BE_Company.dll 2>/dev/null | grep -F 'إبطال المسودة وأرشفة الطلب فقط' | head -3 || echo 'WARN_STRING_CHECK'
echo '=== Gateway existingExceptionId string? ==='
strings /opt/qalaa-runtime/SalesGateway/BE_SalesEmployee.dll 2>/dev/null | grep -F 'existingExceptionId' | head -3 || echo 'no_existingExceptionId_literal'
echo '=== Services up ==='
systemctl is-active qalaa-company-sharenewnajaf.service qalaa-company-sharenewrkarkok.service qalaa-sales-gateway.service
"""

SQL_NAJAF = r"""
SET NOCOUNT ON;
PRINT '=== Clear phantom CustomerSaleId on posted drafts where row missing ===';
UPDATE d
SET CustomerSaleId = NULL
FROM dbo.SalesDrafts d
WHERE d.CustomerSaleId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.CustomersSales cs WHERE cs.CustomerSaleID = d.CustomerSaleId);
SELECT @@ROWCOUNT AS ClearedPhantomLinks;

PRINT '=== Remaining posted missing link ===';
SELECT COUNT(*) AS StillMissing
FROM dbo.SalesDrafts d
WHERE ISNULL(d.PostingStatus,N'') IN (N'Posted', N'posted')
  AND (d.CustomerSaleId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.CustomersSales cs WHERE cs.CustomerSaleID=d.CustomerSaleId));
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
_, out, err = c.exec_command(REMOTE, timeout=60)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR", e[-1000:])

sftp = c.open_sftp()
with sftp.file("/tmp/clear_phantom.sql", "w") as f:
    f.write(SQL_NAJAF)
runner = r"""
import json, subprocess, tempfile, os
from pathlib import Path

def parse_cs(cs):
    parts={}
    for p in (cs or '').split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return (parts.get('server') or parts.get('data source'),
            parts.get('initial catalog') or parts.get('database'),
            parts.get('user id') or parts.get('uid'),
            parts.get('password') or parts.get('pwd'))

for app in ['AdminNajaf','AdminKirkuk']:
    cfg=json.loads(Path(f'/opt/qalaa-runtime/{app}/appsettings.json').read_text())
    server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
    print('====', app, db, '====')
    sql=Path('/tmp/clear_phantom.sql').read_text(encoding='utf-8-sig')
    fd, path = tempfile.mkstemp(suffix='.sql'); os.close(fd)
    Path(path).write_text(sql, encoding='utf-8-sig')
    cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
    for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
      if bin=='sqlcmd' or os.path.isfile(bin):
        cmd[0]=bin; break
    r=subprocess.run(cmd, capture_output=True, text=True)
    print(r.stdout or '')
    if r.stderr: print('STDERR', r.stderr[-1000:])
    os.unlink(path)
"""
with sftp.file("/tmp/clear_phantom_runner.py", "w") as f:
    f.write(runner)
sftp.close()
_, o2, e2 = c.exec_command("python3 /tmp/clear_phantom_runner.py", timeout=90)
print(o2.read().decode("utf-8", "replace"))
print(e2.read().decode("utf-8", "replace")[-500:])
c.close()
