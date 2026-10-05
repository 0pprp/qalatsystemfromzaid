#!/usr/bin/env python3
"""Check if phantom-link drafts' customers have other sales (dangerous fallback)."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

SQL = r"""
SET NOCOUNT ON;
PRINT '=== Posted drafts with null/missing CustomerSaleId + other sales on same customer ===';
SELECT TOP 30
  d.SaleId, d.CustomerId, d.CustomerSaleId, d.Phone, d.PostingStatus,
  (SELECT COUNT(*) FROM dbo.CustomersSales cs WHERE cs.CustomerID = d.CustomerId) AS SalesCnt,
  (SELECT MAX(CustomerSaleID) FROM dbo.CustomersSales cs WHERE cs.CustomerID = d.CustomerId) AS LatestSaleId
FROM dbo.SalesDrafts d
WHERE ISNULL(d.PostingStatus,N'') IN (N'Posted',N'posted')
  AND (
    d.CustomerSaleId IS NULL
    OR NOT EXISTS (SELECT 1 FROM dbo.CustomersSales cs WHERE cs.CustomerSaleID = d.CustomerSaleId)
  )
ORDER BY d.SaleId DESC;

PRINT '=== Draft 66 / request links Najaf ===';
IF OBJECT_ID('dbo.SalesDrafts') IS NOT NULL
BEGIN
  SELECT d.SaleId, d.CustomerId, d.CustomerSaleId, d.PostingStatus,
         (SELECT COUNT(*) FROM dbo.CustomersSales cs WHERE cs.CustomerID=d.CustomerId) AS SalesCnt
  FROM dbo.SalesDrafts d WHERE d.SaleId IN (66,65,64,63);
END
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/cascade_risk.sql", "w") as f:
    f.write(SQL)
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
    sql=Path('/tmp/cascade_risk.sql').read_text(encoding='utf-8-sig')
    fd,path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
    Path(path).write_text(sql, encoding='utf-8-sig')
    cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
    for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
      if bin=='sqlcmd' or os.path.isfile(bin): cmd[0]=bin; break
    r=subprocess.run(cmd, capture_output=True, text=True)
    print(r.stdout or ''); 
    if r.stderr: print(r.stderr[-800:])
    os.unlink(path)
"""
with sftp.file("/tmp/cascade_risk_runner.py", "w") as f:
    f.write(runner)
sftp.close()
_, o, e = c.exec_command("python3 /tmp/cascade_risk_runner.py", timeout=90)
print(o.read().decode("utf-8", "replace"))
c.close()
