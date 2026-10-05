#!/usr/bin/env python3
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

REMOTE = r"""
import re, subprocess
from pathlib import Path

app = Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text(encoding='utf-8', errors='replace')
m = re.search(r'"DataBaseConnection"\s*:\s*"([^"]+)"', app)
cs = m.group(1)
parts = {k.strip().lower(): v.strip() for p in cs.split(';') if '=' in p for k,v in [p.split('=',1)]}
server = parts.get('server') or parts.get('data source')
db = parts.get('database') or parts.get('initial catalog')
user = parts.get('user id') or parts.get('uid')
pwd = parts.get('password') or parts.get('pwd')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'

def run(sql):
    Path('/tmp/q.sql').write_text(sql, encoding='utf-8')
    return subprocess.check_output([sqlcmd,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-h','-1','-W','-i','/tmp/q.sql'], text=True, errors='replace', timeout=120)

sql = r'''
SET NOCOUNT ON;
;WITH Sale AS (
  SELECT CustomerID, MAX(DateCreate) AS DateSaleDevice,
         ROUND(ISNULL(SUM(AmountTotalSalesDenar),0),-3) AS AmountTotalSales
  FROM dbo.View_CustomersSalesDelegate GROUP BY CustomerID
),
Pay AS (
  SELECT CustomerID, ROUND(ISNULL(SUM(AmountDenar),0),-3) AS ReceiptsTotal
  FROM dbo.View_CustomersPaymentsDelegate GROUP BY CustomerID
),
Facts AS (
  SELECT c.CustomerID, c.DelegateID, s.DateSaleDevice,
         ROUND(ISNULL(s.AmountTotalSales,0) - ISNULL(p.ReceiptsTotal,0), -3) AS AmountRemaining
  FROM dbo.Customers c
  LEFT JOIN Sale s ON s.CustomerID = c.CustomerID
  LEFT JOIN Pay p ON p.CustomerID = c.CustomerID
)
SELECT 'open_sold_sep_any_del', COUNT(1) FROM Facts f
WHERE f.AmountRemaining > 0 AND f.DateSaleDevice IS NOT NULL
  AND CONVERT(date,f.DateSaleDevice) BETWEEN '2026-09-01' AND '2026-09-28';

SELECT 'all_sold_sep_any_del', COUNT(1) FROM Facts f
WHERE f.DateSaleDevice IS NOT NULL
  AND CONVERT(date,f.DateSaleDevice) BETWEEN '2026-09-01' AND '2026-09-28';

-- DateSaleDevice in sep but NO CustomersSales row in sep (using CONVERT date on DateCreate)
SELECT 'extra_datesale_vs_cs', COUNT(1) FROM Facts f
WHERE f.DateSaleDevice IS NOT NULL
  AND CONVERT(date,f.DateSaleDevice) BETWEEN '2026-09-01' AND '2026-09-28'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersSales s WHERE s.CustomerID=f.CustomerID
      AND CONVERT(date,s.DateCreate) BETWEEN '2026-09-01' AND '2026-09-28'
  );

-- drafts completed in sep with customer
SELECT 'drafts_done_sep', COUNT(DISTINCT CustomerId) FROM dbo.SalesDrafts
WHERE Status=N'Completed' AND CustomerId IS NOT NULL AND CustomerId>0
  AND CONVERT(date, ISNULL(CompletedAt, CreatedAt)) BETWEEN '2026-09-01' AND '2026-09-28';

-- drafts in sep NOT in customersales sep
SELECT 'drafts_not_in_cs', COUNT(DISTINCT d.CustomerId) FROM dbo.SalesDrafts d
WHERE d.Status=N'Completed' AND d.CustomerId>0
  AND CONVERT(date, ISNULL(d.CompletedAt, d.CreatedAt)) BETWEEN '2026-09-01' AND '2026-09-28'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersSales s WHERE s.CustomerID=d.CustomerId
      AND CONVERT(date,s.DateCreate) BETWEEN '2026-09-01' AND '2026-09-28'
  );
'''
print(run(sql))
"""

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file('/tmp/gap75c.py', 'w') as f:
            f.write(REMOTE)
        sftp.close()
        _, o, e = c.exec_command('python3 /tmp/gap75c.py', timeout=180)
        print(o.read().decode('utf-8', 'replace'))
        err = e.read().decode('utf-8', 'replace')
        if err.strip():
            print('STDERR', err[:1000])
        return o.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == '__main__':
    raise SystemExit(main())
