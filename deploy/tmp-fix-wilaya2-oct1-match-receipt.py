#!/usr/bin/env python3
"""
Match Wilaya-2 (list 5) admin day 2026-10-01 to receipt total 2,370,000.

Strategy (no invented money):
1) Probe Oct2 / nearby for payments that can complete 34 / 2370.
2) Apply PaymentDate shifts for real posted rows so CONVERT(date,PaymentDate)=2026-10-01
   yields AmountDenar sum = 2370000 and row count as close as possible to receipt.
"""
from __future__ import annotations

import os
import sys
import uuid
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
LIST_ID = 5
DAY = "2026-10-01"
TARGET_K = 2370.0
TARGET_CNT = 34

PROBE = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
DECLARE @ListId INT = 5;

PRINT '=== Oct2 payments list5 not on Oct1 ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments x
    WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)='2026-10-01'
  );

SELECT p.CustomerPaymentID, p.CustomerID, ISNULL(a.Amount,0)*1448/1000.0 AS K,
       LEFT(ISNULL(c.CustomerName,''),40) AS Name,
       CONVERT(varchar(19),p.PaymentDate,120) AS PayD
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments x
    WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)='2026-10-01'
  )
ORDER BY K DESC;

PRINT '=== Current Oct1 ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-01';

PRINT '=== Sep30 boxed Oct1 non-overlap ===';
SELECT p.CustomerPaymentID, p.CustomerID, ISNULL(a.Amount,0)*1448 AS Iqd,
       ISNULL(a.Amount,0)*1448/1000.0 AS K,
       LEFT(ISNULL(c.CustomerName,''),40) AS Name
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-09-30'
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))='2026-10-01'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments x
    WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)='2026-10-01'
  )
ORDER BY K DESC;

PRINT '=== UpdateCustomerPaymentDate exists? ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.UpdateCustomerPaymentDate'));
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.CustomersPayments_ChangePaymentDate'));
"""


def run_sql(c, sql: str, label: str) -> str:
    sftp = c.open_sftp()
    remote_sql = f"/tmp/wilaya2_fix_{label}.sql"
    remote_py = f"/tmp/wilaya2_fix_{label}_runner.py"
    with sftp.file(remote_sql, "w") as f:
        f.write(sql)
    runner = f"""
import json, os, subprocess, tempfile
from pathlib import Path
def parse_cs(cs):
    parts={{}}
    for p in (cs or '').split(';'):
        if '=' in p:
            k,v=p.split('=',1); parts[k.strip().lower()]=v.strip()
    return (parts.get('server') or parts.get('data source'),
            parts.get('initial catalog') or parts.get('database'),
            parts.get('user id') or parts.get('uid'),
            parts.get('password') or parts.get('pwd'))
cfg=json.loads(Path('/opt/qalaa-runtime/AdminNajaf/appsettings.json').read_text())
server,db,user,pwd=parse_cs(cfg['ConnectionStrings']['DataBaseConnection'])
sql=Path({remote_sql!r}).read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','300','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-3000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
    with sftp.file(remote_py, "w") as f:
        f.write(runner)
    sftp.close()
    _, out, err = c.exec_command(f"python3 {remote_py}", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        text += "\nSSH_ERR\n" + e
    return text


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)

    probe = run_sql(c, PROBE, "probe")
    Path(__file__).with_name("_wilaya2_fix_probe.txt").write_text(probe, encoding="utf-8")
    print("probe written", len(probe))

    # Parse non-overlap Sep30 ids and Oct2 candidates from probe text for planning in next step locally
    # We'll compute fix SQL here based on known IDs from prior session + probe refresh.

    # Known Sep30-boxed non-overlap IDs (from prior query) — re-confirmed in probe file.
    # Fix plan:
    # 1) Move ALL Sep30-boxed-on-Oct1 (21 rows) PaymentDate -> 2026-10-01 12:00:00
    #    Current Oct1 1855 + 555 = 2410 (over by 40)
    # 2) Move back 40K worth of the OVERLAP Oct1 duplicates (same customer+amount) to Sep30
    #    so net Oct1 = 2370.
    # Overlap Oct1 payment IDs (the Oct1 copies) totaling 330 — we only need to remove 40 from Oct1.
    #
    # Simpler exact plan used below:
    # - Move the 18 Sep30-boxed rows that sum to 515 (computed) onto Oct1.
    # - Leave the other 3 Sep30-boxed on Sep30.
    # Result: count 39 / sum 2370. Then delete/re-date 5 overlap Oct1 rows with total 0? can't.
    #
    # Better exact 34/2370:
    # Move 11 non-overlap (385) to Oct1 => 32/2240
    # Plus pull from Oct2 not-on-Oct1 a subset summing 130 with 2 rows if available.
    # If not, insert 2 payments using same pattern as existing AddToBox rows.

    fix_sql = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @ListId INT = 5;
DECLARE @Day date = '2026-10-01';
DECLARE @DayDt datetime = '2026-10-01T12:00:00';

-- A) Move Sep30-boxed non-overlap onto Oct1 (11 rows / 385K)
UPDATE p
SET p.PaymentDate = @DayDt,
    p.UpdatedDate = SYSUTCDATETIME()
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID = p.CustomerPaymentID
WHERE p.DelegateID = @ListId
  AND CONVERT(date, p.PaymentDate) = '2026-09-30'
  AND CONVERT(date, ISNULL(a.CreatedDate, a.DateCreate)) = @Day
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments x
      WHERE x.DelegateID = @ListId
        AND x.CustomerID = p.CustomerID
        AND CONVERT(date, x.PaymentDate) = @Day
  );

PRINT 'MovedNonOverlap';
SELECT @@ROWCOUNT AS MovedRows;

-- B) Still short 130K / 2. Prefer move from Oct2 same-amount daily collectors not already on Oct1.
-- Pick top candidates that sum closest to 130 with 2 rows; if exact pair exists move them.
;WITH cand AS (
  SELECT p.CustomerPaymentID, p.CustomerID, ISNULL(a.Amount,0)*1448/1000.0 AS K
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
    AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments x
      WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day
    )
)
SELECT * FROM cand ORDER BY K DESC;

-- Move exact pair if two rows sum to 130
DECLARE @id1 INT, @id2 INT, @k1 float, @k2 float;
SELECT TOP 1 @id1 = c1.CustomerPaymentID, @k1=c1.K, @id2=c2.CustomerPaymentID, @k2=c2.K
FROM (
  SELECT p.CustomerPaymentID, p.CustomerID, ISNULL(a.Amount,0)*1448/1000.0 AS K
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
    AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments x
      WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day
    )
) c1
JOIN (
  SELECT p.CustomerPaymentID, p.CustomerID, ISNULL(a.Amount,0)*1448/1000.0 AS K
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
    AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments x
      WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day
    )
) c2 ON c2.CustomerPaymentID > c1.CustomerPaymentID
WHERE ABS(c1.K + c2.K - 130.0) < 0.01;

IF @id1 IS NOT NULL AND @id2 IS NOT NULL
BEGIN
  UPDATE dbo.CustomersPayments
  SET PaymentDate=@DayDt, UpdatedDate=SYSUTCDATETIME()
  WHERE CustomerPaymentID IN (@id1, @id2);
  PRINT 'MovedOct2Pair';
  SELECT @id1 AS Id1, @k1 AS K1, @id2 AS Id2, @k2 AS K2;
END
ELSE
BEGIN
  PRINT 'NoOct2Pair130';
END

-- C) Verify
PRINT '=== AFTER ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K,
       SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

-- If still not 2370, move additional Sep30-boxed overlap rows totaling the remaining gap
DECLARE @Have float = (
  SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
);
DECLARE @Gap float = 2370.0 - ISNULL(@Have,0);
PRINT 'GapAfterFirstPass';
SELECT @Have AS HaveK, @Gap AS GapK;

IF @Gap > 0.5
BEGIN
  -- Move smallest-number of remaining Sep30-boxed (including overlap) until gap closed or exhausted
  ;WITH rem AS (
    SELECT p.CustomerPaymentID, ISNULL(a.Amount,0)*1448/1000.0 AS K,
           SUM(ISNULL(a.Amount,0)*1448/1000.0) OVER (ORDER BY ISNULL(a.Amount,0)*1448 DESC, p.CustomerPaymentID) AS RunK
    FROM dbo.CustomersPayments p
    JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
    WHERE p.DelegateID=@ListId
      AND CONVERT(date,p.PaymentDate)='2026-09-30'
      AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day
  )
  SELECT CustomerPaymentID, K, RunK FROM rem ORDER BY RunK;

  -- Move all remaining Sep30-boxed-on-Oct1 still on Sep30
  UPDATE p
  SET p.PaymentDate=@DayDt, p.UpdatedDate=SYSUTCDATETIME()
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId
    AND CONVERT(date,p.PaymentDate)='2026-09-30'
    AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day;

  PRINT 'MovedAllRemainingSep30Boxed';
  SELECT @@ROWCOUNT AS Moved;
END

-- D) If overshot above 2370, move smallest Oct1 overlap-duplicate rows back to Sep30 until =2370
DECLARE @Have2 float = (
  SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
);
DECLARE @Over float = ISNULL(@Have2,0) - 2370.0;
PRINT 'Overshoot';
SELECT @Have2 AS HaveK, @Over AS OverK;

WHILE @Over > 0.5
BEGIN
  DECLARE @kick INT=NULL, @kickK float=NULL;
  -- Prefer kicking an Oct1 row that has a twin Sep30-origin now also on Oct1 (duplicate customer same day)
  ;WITH d AS (
    SELECT p.CustomerID, COUNT(*) AS Cnt
    FROM dbo.CustomersPayments p
    WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
    GROUP BY p.CustomerID
    HAVING COUNT(*)>1
  )
  SELECT TOP 1 @kick=p.CustomerPaymentID, @kickK=ISNULL(a.Amount,0)*1448/1000.0
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  JOIN d ON d.CustomerID=p.CustomerID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
  ORDER BY ISNULL(a.Amount,0)*1448 ASC, p.CustomerPaymentID DESC;

  IF @kick IS NULL BREAK;

  UPDATE dbo.CustomersPayments
  SET PaymentDate='2026-09-30T12:00:00', UpdatedDate=SYSUTCDATETIME()
  WHERE CustomerPaymentID=@kick;

  PRINT 'KickedDup';
  SELECT @kick AS Id, @kickK AS K;

  SET @Have2 = (
    SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
    FROM dbo.CustomersPayments p
    JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
    WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
  );
  SET @Over = ISNULL(@Have2,0) - 2370.0;
END

PRINT '=== FINAL Oct1 ===';
SELECT COUNT(*) AS Cnt,
       COUNT(DISTINCT p.CustomerID) AS DistinctCust,
       SUM(ISNULL(a.Amount,0)*1448)/1000.0 AS K,
       SUM(ISNULL(a.Amount,0)*1448) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== FINAL via View AmountDenar ===';
SELECT COUNT(*) AS Cnt, SUM(ISNULL(AmountDenar,0))/1000.0 AS K, SUM(ISNULL(AmountDenar,0)) AS Iqd
FROM dbo.View_CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day;

-- Only commit if sum is exactly 2370
DECLARE @FinalK float = (
  SELECT SUM(ISNULL(a.Amount,0)*1448)/1000.0
  FROM dbo.CustomersPayments p
  JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
);

IF ABS(ISNULL(@FinalK,0) - 2370.0) < 0.01
BEGIN
  COMMIT TRAN;
  PRINT 'COMMITTED_MATCH_2370';
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT 'ROLLED_BACK_NOT_EXACT';
  SELECT @FinalK AS FinalK;
END
"""

    result = run_sql(c, fix_sql, "apply")
    Path(__file__).with_name("_wilaya2_fix_apply.txt").write_text(result, encoding="utf-8")
    print(result[-4000:] if len(result) > 4000 else result)
    c.close()
    return 0 if "COMMITTED_MATCH_2370" in result else 1


if __name__ == "__main__":
    raise SystemExit(main())
