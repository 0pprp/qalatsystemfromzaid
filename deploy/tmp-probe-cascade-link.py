#!/usr/bin/env python3
"""Probe posted drafts with missing CustomerSaleId link (cascade void failure)."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;

PRINT '=== Posted drafts missing usable CustomerSaleId (Najaf) ===';
SELECT TOP 30
  d.SaleId, d.Status, d.PostingStatus, d.CustomerId, d.CustomerSaleId, d.PostedAtUtc, d.CompletedAt,
  cs.CustomerSaleID AS ResolvedByCustomerSaleId
FROM dbo.SalesDrafts d
LEFT JOIN dbo.CustomersSales cs ON cs.CustomerSaleID = d.CustomerSaleId
WHERE ISNULL(d.PostingStatus, N'') IN (N'Posted', N'posted')
  AND (d.CustomerSaleId IS NULL OR cs.CustomerSaleID IS NULL)
ORDER BY d.SaleId DESC;

PRINT '=== Count posted missing link ===';
SELECT COUNT(*) AS MissingLink
FROM dbo.SalesDrafts d
LEFT JOIN dbo.CustomersSales cs ON cs.CustomerSaleID = d.CustomerSaleId
WHERE ISNULL(d.PostingStatus, N'') IN (N'Posted', N'posted')
  AND (d.CustomerSaleId IS NULL OR cs.CustomerSaleID IS NULL);

PRINT '=== SalesRequests Completed with ConvertedToSaleId where draft missing link ===';
SELECT TOP 20
  r.Id AS RequestId, r.CustomerName, r.CustomerPhone, r.Status, r.ConvertedToSaleId,
  d.CustomerId, d.CustomerSaleId, d.PostingStatus,
  (SELECT COUNT(*) FROM dbo.CustomersSales x WHERE x.CustomerID = d.CustomerId) AS SalesForCustomer
FROM dbo.SalesRequests r
JOIN dbo.SalesDrafts d ON d.SaleId = r.ConvertedToSaleId
WHERE r.Status = N'Completed'
  AND ISNULL(r.IsDeleted,0)=0
  AND ISNULL(d.PostingStatus,N'') IN (N'Posted', N'posted')
  AND (d.CustomerSaleId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.CustomersSales cs WHERE cs.CustomerSaleID=d.CustomerSaleId))
ORDER BY r.Id DESC;

PRINT '=== Draft columns ===';
SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.SalesDrafts') AND name LIKE '%Customer%' OR (object_id=OBJECT_ID('dbo.SalesDrafts') AND name IN ('Phone','FullName','SaleId','PostingStatus','Status'))
ORDER BY name;
"""

RUNNER = r"""
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

# Check Najaf and Kirkuk
for app in ['AdminNajaf','AdminKirkuk']:
    cfg_path=f'/opt/qalaa-runtime/{app}/appsettings.json'
    if not Path(cfg_path).exists():
        print('skip', app); continue
    cfg=json.loads(Path(cfg_path).read_text())
    server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
    print('====', app, db, '====')
    sql=Path('/tmp/cascade_probe.sql').read_text(encoding='utf-8-sig')
    fd, path = tempfile.mkstemp(suffix='.sql'); os.close(fd)
    Path(path).write_text(sql, encoding='utf-8-sig')
    cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
    for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
      if bin=='sqlcmd' or os.path.isfile(bin):
        cmd[0]=bin; break
    r=subprocess.run(cmd, capture_output=True, text=True)
    print(r.stdout or '')
    if r.stderr: print('STDERR', r.stderr[-1500:])
    os.unlink(path)
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/cascade_probe.sql", "w") as f:
    f.write(SQL)
with sftp.file("/tmp/cascade_probe_runner.py", "w") as f:
    f.write(RUNNER)
sftp.close()
_, out, err = c.exec_command("python3 /tmp/cascade_probe_runner.py", timeout=120)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR", e[-1500:])
c.close()
