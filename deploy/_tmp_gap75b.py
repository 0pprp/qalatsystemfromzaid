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
    cmd = [sqlcmd,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-h','-1','-W','-i','/tmp/q.sql']
    return subprocess.check_output(cmd, text=True, errors='replace', timeout=90)

# columns of SalesDrafts
print('COLS', run("SET NOCOUNT ON; SELECT TOP 1 * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SalesDrafts' ORDER BY ORDINAL_POSITION; SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SalesDrafts' ORDER BY ORDINAL_POSITION;"))

# rating-like: open balance + max sale date in sep via same OUTER APPLY as RatingDataSource
sql = '''
SET NOCOUNT ON;
;WITH Sale AS (
  SELECT CustomerID, MAX(DateCreate) AS DateSaleDevice,
         ROUND(ISNULL(SUM(AmountTotalSalesDenar),0),-3) AS AmountTotalSales
  FROM dbo.View_CustomersSalesDelegate
  GROUP BY CustomerID
),
Pay AS (
  SELECT CustomerID, MAX(PaymentDate) AS LastPaymentDate,
         ROUND(ISNULL(SUM(AmountDenar),0),-3) AS ReceiptsTotal
  FROM dbo.View_CustomersPaymentsDelegate
  GROUP BY CustomerID
),
Facts AS (
  SELECT c.CustomerID, c.DelegateID, s.DateSaleDevice,
         ROUND(ISNULL(s.AmountTotalSales,0) - ISNULL(p.ReceiptsTotal,0), -3) AS AmountRemaining
  FROM dbo.Customers c
  LEFT JOIN Sale s ON s.CustomerID = c.CustomerID
  LEFT JOIN Pay p ON p.CustomerID = c.CustomerID
)
SELECT 'open_sold_sep', COUNT(1)
FROM Facts f
INNER JOIN dbo.Delegates d ON d.DelegateID = f.DelegateID AND ISNULL(d.DelegateState,1) = 1
WHERE f.AmountRemaining > 0
  AND f.DateSaleDevice IS NOT NULL
  AND CONVERT(date, f.DateSaleDevice) >= '2026-09-01'
  AND CONVERT(date, f.DateSaleDevice) <= '2026-09-28';

SELECT 'all_sold_sep_facts', COUNT(1)
FROM Facts f
INNER JOIN dbo.Delegates d ON d.DelegateID = f.DelegateID AND ISNULL(d.DelegateState,1) = 1
WHERE f.DateSaleDevice IS NOT NULL
  AND CONVERT(date, f.DateSaleDevice) >= '2026-09-01'
  AND CONVERT(date, f.DateSaleDevice) <= '2026-09-28';

-- customers in rating-style but NOT in CustomersSales DateCreate sep
SELECT 'extra_vs_cs', COUNT(1)
FROM Facts f
INNER JOIN dbo.Delegates d ON d.DelegateID = f.DelegateID AND ISNULL(d.DelegateState,1) = 1
WHERE f.AmountRemaining > 0
  AND f.DateSaleDevice IS NOT NULL
  AND CONVERT(date, f.DateSaleDevice) >= '2026-09-01'
  AND CONVERT(date, f.DateSaleDevice) <= '2026-09-28'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersSales s
    WHERE s.CustomerID = f.CustomerID
      AND s.DateCreate >= '2026-09-01' AND s.DateCreate < '2026-10-01'
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
        with sftp.file('/tmp/gap75b.py', 'w') as f:
            f.write(REMOTE)
        sftp.close()
        _, stdout, stderr = c.exec_command('python3 /tmp/gap75b.py', timeout=180)
        print(stdout.read().decode('utf-8', 'replace')[-4000:])
        err = stderr.read().decode('utf-8', 'replace')
        if err.strip():
            print('STDERR', err[:1500])
        return stdout.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == '__main__':
    raise SystemExit(main())
