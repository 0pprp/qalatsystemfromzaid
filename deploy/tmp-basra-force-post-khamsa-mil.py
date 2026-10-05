#!/usr/bin/env python3
"""Probe Basra pending collection payments for list خمسة ميل الأولى."""
from __future__ import annotations

import json
import os
import subprocess
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
LIST_NAME = "خمسة ميل الأولى"
APPLY = "--apply" in sys.argv


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    apply_flag = "1" if APPLY else "0"
    list_name = LIST_NAME

    remote = f"""
import json, subprocess, os, tempfile
apply = {apply_flag}
list_name = {list_name!r}
cfg_path = '/opt/qalaa-runtime/AdminBasra/appsettings.json'
cfg = json.load(open(cfg_path, encoding='utf-8'))
cs = cfg['ConnectionStrings']['DataBaseConnection']
parts = {{}}
for part in cs.split(';'):
    if '=' in part:
        k,v = part.split('=',1)
        parts[k.strip().lower()] = v.strip()
server = parts.get('server') or parts.get('data source')
db = parts.get('initial catalog') or parts.get('database')
user = parts.get('user id') or parts.get('uid')
pwd = parts.get('password') or parts.get('pwd')
print('DB', db, 'SERVER', server)

sqlcmd = ['/opt/mssql-tools18/bin/sqlcmd','-C','-S',server,'-d',db,'-U',user,'-P',pwd,'-W','-s','|','-h','-1']

def run_sql(sql, label):
    path = f'/tmp/basra_pay_{{label}}.sql'
    open(path,'w',encoding='utf-8').write(sql)
    r = subprocess.run(sqlcmd + ['-i', path], capture_output=True, text=True)
    print('===', label, 'RC', r.returncode, '===')
    out = (r.stdout or '') + (('\\nSTDERR '+r.stderr) if r.stderr else '')
    print(out[:8000])
    return r.returncode, out

# 1) Find delegate
run_sql(f'''SET NOCOUNT ON;
SELECT DelegateID, DelegateName, ISNULL(DelegateState,0) AS St
FROM dbo.Delegates
WHERE DelegateName LIKE N'%{{list_name}}%' OR DelegateName LIKE N'%خمسة ميل%';
''', 'delegates')

# 2) Pending auto-post for matching lists
run_sql(f'''SET NOCOUNT ON;
SELECT d.DelegateID, d.DelegateName,
  COUNT(*) AS Cnt,
  CAST(SUM(cpr.Amount) AS bigint) AS SumAmount,
  SUM(CASE WHEN cpr.PostedToBoxAtUtc IS NULL THEN 1 ELSE 0 END) AS Unposted,
  MIN(cpr.EligibleForPostingAtUtc) AS MinElig,
  MAX(cpr.EligibleForPostingAtUtc) AS MaxElig,
  MIN(ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate)) AS MinCreated,
  MAX(ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate)) AS MaxCreated
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE (d.DelegateName LIKE N'%{{list_name}}%' OR d.DelegateName LIKE N'%خمسة ميل%')
  AND ISNULL(cpr.AutoPostEnabled,0)=1
GROUP BY d.DelegateID, d.DelegateName
ORDER BY SumAmount DESC;
''', 'summary')

# 3) Detail rows matching ~15 / 379000 if present
run_sql(f'''SET NOCOUNT ON;
;WITH tgt AS (
  SELECT d.DelegateID
  FROM dbo.Delegates d
  WHERE d.DelegateName LIKE N'%{{list_name}}%' OR d.DelegateName LIKE N'%خمسة ميل الأولى%'
)
SELECT cpr.CustomersPaymentsRequestID, c.CustomerID, c.CustomerName,
  CAST(cpr.Amount AS bigint) AS Amount,
  cpr.AutoPostEnabled, cpr.PostedToBoxAtUtc, cpr.EligibleForPostingAtUtc,
  cpr.ClientPaymentId, cpr.PaymentDate, cpr.CreatedAtUtc
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID IN (SELECT DelegateID FROM tgt)
  AND ISNULL(cpr.AutoPostEnabled,0)=1
  AND cpr.PostedToBoxAtUtc IS NULL
ORDER BY cpr.CustomersPaymentsRequestID;
''', 'detail')

# 4) Distinct customer names + totals for unposted
run_sql(f'''SET NOCOUNT ON;
;WITH tgt AS (
  SELECT d.DelegateID, d.DelegateName
  FROM dbo.Delegates d
  WHERE d.DelegateName LIKE N'%{{list_name}}%' OR d.DelegateName LIKE N'%خمسة ميل%'
)
SELECT COUNT(*) AS Cnt, CAST(SUM(cpr.Amount) AS bigint) AS SumAmount,
  COUNT(DISTINCT c.CustomerID) AS DistinctCustomers
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID IN (SELECT DelegateID FROM tgt)
  AND ISNULL(cpr.AutoPostEnabled,0)=1
  AND cpr.PostedToBoxAtUtc IS NULL;

SELECT TOP 20 c.CustomerName, COUNT(*) AS Cnt, CAST(SUM(cpr.Amount) AS bigint) AS SumAmount
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
WHERE c.DelegateID IN (SELECT DelegateID FROM tgt)
  AND ISNULL(cpr.AutoPostEnabled,0)=1
  AND cpr.PostedToBoxAtUtc IS NULL
GROUP BY c.CustomerName
ORDER BY Cnt DESC;
''', 'names')

if apply:
    # Force eligibility to now for unposted auto rows on this list only, then post.
    run_sql(f'''SET NOCOUNT ON;
DECLARE @NowUtc DATETIME2 = SYSUTCDATETIME();
DECLARE @Ids TABLE (Id INT PRIMARY KEY);

INSERT INTO @Ids(Id)
SELECT cpr.CustomersPaymentsRequestID
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE (d.DelegateName LIKE N'%{{list_name}}%' OR d.DelegateName LIKE N'%خمسة ميل الأولى%')
  AND ISNULL(cpr.AutoPostEnabled,0)=1
  AND cpr.PostedToBoxAtUtc IS NULL;

DECLARE @Cnt INT = (SELECT COUNT(*) FROM @Ids);
DECLARE @Sum BIGINT = (
  SELECT CAST(SUM(cpr.Amount) AS bigint)
  FROM dbo.CustomersPaymentsRequest cpr
  WHERE cpr.CustomersPaymentsRequestID IN (SELECT Id FROM @Ids)
);
PRINT CONCAT('TARGET_CNT=', @Cnt, ' TARGET_SUM=', @Sum);

-- Safety: only proceed if matches expected window (15 / ~379000) OR user forced via apply
IF @Cnt = 0
BEGIN
  PRINT 'NOTHING_TO_POST';
  RETURN;
END

UPDATE dbo.CustomersPaymentsRequest
SET EligibleForPostingAtUtc = DATEADD(MINUTE, -1, @NowUtc),
    UpdatedDate = GETDATE()
WHERE CustomersPaymentsRequestID IN (SELECT Id FROM @Ids)
  AND PostedToBoxAtUtc IS NULL;

DECLARE @Posted INT;
EXEC dbo.CustomersPaymentsRequest_PostEligible @UserCreateID = 1, @PostedCount = @Posted OUTPUT;
PRINT CONCAT('POSTED_COUNT=', ISNULL(@Posted, -1));

SELECT COUNT(*) AS RemainingUnposted,
  CAST(ISNULL(SUM(cpr.Amount),0) AS bigint) AS RemainingSum
FROM dbo.CustomersPaymentsRequest cpr
JOIN dbo.Customers c ON c.CustomerID = cpr.CustomerID
JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE (d.DelegateName LIKE N'%{{list_name}}%' OR d.DelegateName LIKE N'%خمسة ميل الأولى%')
  AND ISNULL(cpr.AutoPostEnabled,0)=1
  AND cpr.PostedToBoxAtUtc IS NULL;
''', 'apply_post')
"""

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/basra_force_post_probe.py", "w") as f:
                f.write(remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/basra_force_post_probe.py", timeout=180)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write((out + ("\nSTDERR " + err[:1500] if err.strip() else "") + "\n").encode("utf-8", "replace"))
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
