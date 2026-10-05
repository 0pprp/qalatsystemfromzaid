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
Draft AS (
  SELECT CustomerId AS CustomerID,
         MAX(COALESCE(CompletedAt, CreatedAt)) AS SaleDate,
         CAST(SUM(FinalSalePrice) AS float) AS AmountTotal,
         CAST(SUM(CASE WHEN DownPayment > 0 THEN DownPayment ELSE 0 END) AS float) AS DownPayment
  FROM dbo.SalesDrafts
  WHERE CustomerId IS NOT NULL AND CustomerId > 0
    AND Status IN (N'Completed', N'DocumentsReady', N'DocumentsPending')
    AND FinalSalePrice > 0
  GROUP BY CustomerId
),
Facts AS (
  SELECT c.CustomerID, c.DelegateID,
         COALESCE(s.DateSaleDevice, d.SaleDate) AS DateSaleDevice,
         CASE WHEN ISNULL(s.AmountTotalSales,0) > 0 THEN ISNULL(s.AmountTotalSales,0) ELSE ISNULL(d.AmountTotal,0) END AS AmountTotalSales,
         CASE
           WHEN ISNULL(s.AmountTotalSales,0) > 0 THEN ROUND(ISNULL(s.AmountTotalSales,0) - ISNULL(p.ReceiptsTotal,0), -3)
           ELSE ROUND(ISNULL(d.AmountTotal,0) - ISNULL(d.DownPayment,0), -3)
         END AS AmountRemaining
  FROM dbo.Customers c
  LEFT JOIN Sale s ON s.CustomerID = c.CustomerID
  LEFT JOIN Pay p ON p.CustomerID = c.CustomerID
  LEFT JOIN Draft d ON d.CustomerID = c.CustomerID
)
SELECT 'enriched_open_sep' AS k, COUNT(1) AS c FROM Facts
WHERE AmountRemaining > 0 AND DateSaleDevice IS NOT NULL
  AND CONVERT(date, DateSaleDevice) BETWEEN '2026-09-01' AND '2026-09-28'
UNION ALL
SELECT 'enriched_all_sep', COUNT(1) FROM Facts
WHERE DateSaleDevice IS NOT NULL
  AND CONVERT(date, DateSaleDevice) BETWEEN '2026-09-01' AND '2026-09-28'
UNION ALL
SELECT 'union_cs_draft_sep', COUNT(*) FROM (
  SELECT CustomerID FROM dbo.CustomersSales WHERE CONVERT(date,DateCreate) BETWEEN '2026-09-01' AND '2026-09-28'
  UNION
  SELECT CustomerId FROM dbo.SalesDrafts
  WHERE CustomerId>0 AND Status IN (N'Completed', N'DocumentsReady', N'DocumentsPending')
    AND CONVERT(date, ISNULL(CompletedAt, CreatedAt)) BETWEEN '2026-09-01' AND '2026-09-28'
) u;
'''
Path('/tmp/q.sql').write_text(sql, encoding='utf-8')
print(subprocess.check_output([sqlcmd,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-h','-1','-W','-s',',','-i','/tmp/q.sql'], text=True, errors='replace', timeout=120))
"""

def main() -> int:
    if not SSH_PASS:
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username='root', password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file('/tmp/gap75d.py', 'w') as f:
            f.write(REMOTE)
        sftp.close()
        _, o, e = c.exec_command('python3 /tmp/gap75d.py', timeout=180)
        print(o.read().decode('utf-8', 'replace'))
        err = e.read().decode('utf-8', 'replace')
        if err.strip():
            print('STDERR', err[:1000])
        return o.channel.recv_exit_status()
    finally:
        c.close()

if __name__ == '__main__':
    raise SystemExit(main())
