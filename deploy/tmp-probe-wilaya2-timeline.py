#!/usr/bin/env python3
"""Check Delegate API health, logs, and Wilaya-2 sync timeline."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r"""
set -e
echo '=== Runtime dirs ==='
ls /opt/qalaa-runtime | head -40
echo '=== Najaf delegate / admin processes ==='
ps aux | grep -E 'Delegate|AdminNajaf|BE_Company|dotnet' | grep -v grep | head -40
echo '=== systemd units ==='
systemctl list-units --type=service --all 2>/dev/null | grep -iE 'najaf|delegate|qalaa|company' | head -40
echo '=== Recent journal AdminNajaf / DelegateNajaf ==='
journalctl --no-pager -n 80 -u 'qalaa-AdminNajaf*' 2>/dev/null || true
journalctl --no-pager -n 80 -u '*Najaf*' 2>/dev/null | tail -80 || true
echo '=== Log files ==='
find /opt/qalaa-runtime -name '*.log' 2>/dev/null | head -40
find /var/log -iname '*najaf*' 2>/dev/null | head -20
find /var/log -iname '*delegate*' 2>/dev/null | head -20
echo '=== nginx / caddy sites najaf ==='
ls /etc/nginx/sites-enabled 2>/dev/null | head -40 || true
grep -l najaf /etc/nginx/sites-enabled/* 2>/dev/null | head -10 || true
"""

SQL = r"""
SET NOCOUNT ON;
DECLARE @ListId INT = 5;
DECLARE @Today date = CONVERT(date, DATEADD(HOUR,3,SYSUTCDATETIME()));

PRINT '=== Today payments hourly (Baghdad) for list 5 ===';
SELECT DATEPART(hour, DATEADD(HOUR,3,CASE WHEN p.PaymentDate AT TIME ZONE 'UTC' IS NOT NULL THEN p.PaymentDate ELSE p.PaymentDate END)) AS RoughHour,
       COUNT(*) AS Cnt,
       SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Today
GROUP BY DATEPART(hour, p.PaymentDate)
ORDER BY 1;

PRINT '=== PaymentDate raw vs CreatedDate today ===';
SELECT TOP 20 p.CustomerPaymentID, p.PaymentDate, p.CreatedDate, p.ClientPaymentId,
       ISNULL(a.Amount,0)*1448 AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND (
  CONVERT(date,p.PaymentDate)=@Today OR CONVERT(date,p.CreatedDate)=@Today
)
ORDER BY p.CustomerPaymentID DESC;

PRINT '=== Last 30 payments list 5 any day ===';
SELECT TOP 30 p.CustomerPaymentID, p.PaymentDate, p.CreatedDate, p.ClientPaymentId,
       ISNULL(a.Amount,0)*1448 AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
ORDER BY p.CustomerPaymentID DESC;

PRINT '=== Compare list 4 (ولاية 1) today ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
LEFT JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=4 AND CONVERT(date,p.PaymentDate)=@Today;

PRINT '=== PostEligible SP definition snippet (filter) ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.CustomersPaymentsRequest_PostEligible'));
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_timeline.sql", "w") as f:
    f.write(SQL)
sftp.close()

_, out, err = c.exec_command(REMOTE, timeout=60)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("SSH_ERR_REMOTE", e[-1500:])

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

cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
sql=Path('/tmp/wilaya2_timeline.sql').read_text(encoding='utf-8-sig')
fd, path = tempfile.mkstemp(suffix='.sql')
os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
cmd=['sqlcmd','-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-s','|','-i',path]
for bin in ['/opt/mssql-tools18/bin/sqlcmd','/opt/mssql-tools/bin/sqlcmd','sqlcmd']:
  if bin=='sqlcmd' or os.path.isfile(bin):
    cmd[0]=bin
    break
r=subprocess.run(cmd, capture_output=True, text=True)
print((r.stdout or '')[:25000])
if r.stderr:
  print('STDERR', r.stderr[-2000:])
os.unlink(path)
"""
sftp = c.open_sftp()
with sftp.file("/tmp/wilaya2_timeline_runner.py", "w") as f:
    f.write(runner)
sftp.close()
_, out2, err2 = c.exec_command("python3 /tmp/wilaya2_timeline_runner.py", timeout=120)
print(out2.read().decode("utf-8", "replace"))
e2 = err2.read().decode("utf-8", "replace")
if e2.strip():
    print("SSH_ERR_SQL", e2[-1500:])
c.close()
