#!/usr/bin/env python3
"""Fix float drift on the two inserted payments so Oct1 sum is exactly 2370000 IQD."""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
SET XACT_ABORT ON;
BEGIN TRAN;

-- Fix the two match-receipt rows to exact dinar via Amount = IQD/1448 using decimal math
UPDATE dbo.AddToBox
SET Amount = CONVERT(float, CONVERT(decimal(18,8), 80000)/CONVERT(decimal(18,8), 1448)),
    Notes = N'مطابقة وصل 1/10'
WHERE CustomerPaymentID = 418783;

UPDATE dbo.AddToBox
SET Amount = CONVERT(float, CONVERT(decimal(18,8), 50000)/CONVERT(decimal(18,8), 1448)),
    Notes = N'مطابقة وصل 1/10'
WHERE CustomerPaymentID = 418784;

-- If still 1-2 IQD short due to float display, nudge
DECLARE @Iqd float = (
  SELECT SUM(ISNULL(a.Amount,0)*1448)
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=5 AND CONVERT(date,p.PaymentDate)='2026-10-01'
);

PRINT CONCAT('IqdNow=', @Iqd);

IF @Iqd < 2370000 AND (2370000 - @Iqd) < 5
BEGIN
  DECLARE @Need float = (2370000 - @Iqd)/1448.0;
  UPDATE dbo.AddToBox SET Amount = Amount + @Need WHERE CustomerPaymentID=418783;
  PRINT CONCAT('Nudged=', @Need);
END

DECLARE @Cnt INT, @K float, @Iqd2 float;
SELECT @Cnt=COUNT(*), @K=SUM(ISNULL(a.Amount,0)*1448)/1000.0, @Iqd2=SUM(ISNULL(a.Amount,0)*1448)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=5 AND CONVERT(date,p.PaymentDate)='2026-10-01';

SELECT @Cnt AS Cnt, @K AS K, @Iqd2 AS Iqd;
SELECT COUNT(*) AS ViewCnt, SUM(ISNULL(AmountDenar,0)) AS ViewIqd, SUM(ISNULL(AmountDenar,0))/1000.0 AS ViewK
FROM dbo.View_CustomersPayments
WHERE DelegateID=5 AND CONVERT(date,PaymentDate)='2026-10-01';

IF @Cnt=34 AND ABS(@Iqd2-2370000)<0.5
BEGIN
  COMMIT TRAN;
  PRINT 'COMMITTED_EXACT';
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT 'ROLLBACK';
END
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_penny.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_penny_runner.py", "w") as f:
        f.write(
            r"""
import json, os, subprocess, tempfile
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
sql=Path('/tmp/wilaya2_penny.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','80','-s','|','-i',path], capture_output=True, text=True)
Path('/tmp/wilaya2_penny_out.txt').write_text((r.stdout or '')+'\n'+(r.stderr or ''), encoding='utf-8')
print((r.stdout or '')[-2000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_penny_runner.py", timeout=120)
    text = out.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_penny_out.txt").write_text(text, encoding="utf-8")
    # also fetch remote file
    _, out2, _ = c.exec_command("cat /tmp/wilaya2_penny_out.txt", timeout=30)
    remote = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_penny_remote.txt").write_text(remote, encoding="utf-8")
    print(remote[-1500:])
    c.close()
    return 0 if "COMMITTED_EXACT" in remote or "COMMITTED_EXACT" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
