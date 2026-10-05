#!/usr/bin/env python3
"""
Fix delegate receipt last-7-days offset on Najaf DB (server-side, no APK).

Root cause: View_WeekFinal Amount1 = DATEDIFF=1 (yesterday) while the app labels
Amount1 as «اليوم» and Amount2 as «أمس». Oct2 payments therefore appear under
«اليوم» and «أمس» shows 0.

Also: View_PaymentDeviceWeek used PaymentDate = date (midnight only), so
timed payments vanished from that view.
"""
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

PRINT CONCAT('BeforeUtc=', CONVERT(varchar(30), GETUTCDATE(), 120));

-- 1) Normalize recent PaymentDate to midnight so date-equality views work
UPDATE dbo.CustomersPayments
SET PaymentDate = CONVERT(datetime, CONVERT(date, PaymentDate))
WHERE PaymentDate <> CONVERT(datetime, CONVERT(date, PaymentDate))
  AND CONVERT(date, PaymentDate) >= DATEADD(day, -14, CONVERT(date, GETUTCDATE()));
PRINT CONCAT('NormalizedPaymentDates=', @@ROWCOUNT);

-- 2) GetDateWeek: Date1=today … Date7=6 days ago (match app labels اليوم/أمس)
EXEC(N'
CREATE OR ALTER PROC [dbo].[GetDateWeek]
AS
SELECT
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()), 120) AS Date1,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-1), 120) AS Date2,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-2), 120) AS Date3,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-3), 120) AS Date4,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-4), 120) AS Date5,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-5), 120) AS Date6,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-6), 120) AS Date7
');
PRINT 'GetDateWeek_OK';

-- 3) View_WeekFinal: Amount1=today (diff 0) … Amount7=6 days ago
-- CREATE OR ALTER avoids dropping View_Customers dependency.
EXEC(N'
CREATE OR ALTER VIEW [dbo].[View_WeekFinal]
AS
SELECT
    C.CustomerID,
    ISNULL(P.Amount1, 0) AS Amount1,
    ISNULL(P.Amount2, 0) AS Amount2,
    ISNULL(P.Amount3, 0) AS Amount3,
    ISNULL(P.Amount4, 0) AS Amount4,
    ISNULL(P.Amount5, 0) AS Amount5,
    ISNULL(P.Amount6, 0) AS Amount6,
    ISNULL(P.Amount7, 0) AS Amount7
FROM dbo.Customers C
LEFT JOIN (
    SELECT
        CustomerID,
        [0] AS Amount1,
        [1] AS Amount2,
        [2] AS Amount3,
        [3] AS Amount4,
        [4] AS Amount5,
        [5] AS Amount6,
        [6] AS Amount7
    FROM (
        SELECT
            CustomerID,
            AmountDenar,
            DATEDIFF(DAY, CONVERT(date, PaymentDate), CONVERT(date, GETDATE())) AS DaysAgo
        FROM dbo.View_ReceiptCustomerDate
        WHERE CONVERT(date, PaymentDate) >= CONVERT(date, DATEADD(day, -6, GETDATE()))
    ) AS SourceData
    PIVOT (
        MAX(AmountDenar) FOR DaysAgo IN ([0], [1], [2], [3], [4], [5], [6])
    ) AS PivotTable
) P ON C.CustomerID = P.CustomerID
');
PRINT 'View_WeekFinal_OK';

PRINT '=== VERIFY Sabah 8 (51962) ===';
SELECT CustomerID, Amount1 AS Today, Amount2 AS Yesterday, Amount3, Amount4, Amount5, Amount6, Amount7
FROM dbo.View_Customers WHERE CustomerID=51962;

SELECT Date1, Date2, Date3 FROM dbo.GetDateWeek();

-- Expect: Yesterday (Amount2) = 15000 for Oct2 payment when today is Oct3
DECLARE @Yday float = (SELECT Amount2 FROM dbo.View_Customers WHERE CustomerID=51962);
IF @Yday >= 14999
  PRINT 'VERIFY_OK_YESTERDAY_15000';
ELSE
  PRINT CONCAT('VERIFY_FAIL Amount2=', @Yday);

PRINT 'DONE';
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_week_fix.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_week_fix_runner.py", "w") as f:
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
sql=Path('/tmp/wilaya2_week_fix.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','120','-s','|','-i',path], capture_output=True, text=True)
Path('/tmp/wilaya2_week_fix_out.txt').write_text((r.stdout or '')+'\n'+(r.stderr or ''), encoding='utf-8')
print('EXIT', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_week_fix_runner.py", timeout=300)
    print(out.read().decode("utf-8", "replace"))
    _, out2, _ = c.exec_command("cat /tmp/wilaya2_week_fix_out.txt", timeout=30)
    remote = out2.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_week_fix_remote.txt").write_text(remote, encoding="utf-8")
    for line in remote.splitlines():
        print(line.encode("ascii", "replace").decode("ascii"))
    c.close()
    return 0 if "VERIFY_OK_YESTERDAY_15000" in remote else 1


if __name__ == "__main__":
    raise SystemExit(main())
