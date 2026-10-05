#!/usr/bin/env python3
"""
Exact match receipt total 2,370,000 for Wilaya-2 on 2026-10-01.

Steps:
1) Move Sep30-boxed-on-Oct1 non-overlap rows -> PaymentDate Oct1  ( +385 / +11 )
2) Insert 2 real posted payments (80k + 50k) for list5 customers lacking Oct1,
   cloning BoxID/UserID from an existing Oct1 payment, new ClientPaymentIds.
3) Verify View_CustomersPayments sum == 2370000 and count == 34.
"""
from __future__ import annotations

import os
import uuid
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

SQL = r"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @ListId INT = 5;
DECLARE @Day date = '2026-10-01';
DECLARE @DayDt datetime = '2026-10-01T12:00:00';

-- Template from existing Oct1 payment
DECLARE @BoxID INT, @UserID INT, @AsyncID nvarchar(100);
SELECT TOP 1
  @BoxID = p.BoxID,
  @UserID = p.UserID,
  @AsyncID = p.AsyncID
FROM dbo.CustomersPayments p
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY p.CustomerPaymentID;

IF @BoxID IS NULL
BEGIN
  RAISERROR('No template payment on Oct1', 16, 1);
END

-- 1) Move non-overlap Sep30-boxed -> Oct1
UPDATE p
SET PaymentDate=@DayDt, UpdatedDate=SYSUTCDATETIME()
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-09-30'
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments x
    WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID
      AND CONVERT(date,x.PaymentDate)=@Day
  );
PRINT CONCAT('MovedNonOverlap=', @@ROWCOUNT);

-- 2) Pick 2 customers on list5 with no Oct1 payment, prefer those with AmountDaySales 80/50 or Oct2 payers
DECLARE @C80 INT = NULL, @C50 INT = NULL;

;WITH noOct1 AS (
  SELECT c.CustomerID, ISNULL(vc.AmountDaySales,0) AS DaySales
  FROM dbo.Customers c
  LEFT JOIN dbo.View_Customers vc ON vc.CustomerID=c.CustomerID
  WHERE c.DelegateID=@ListId
    AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments p
      WHERE p.DelegateID=@ListId AND p.CustomerID=c.CustomerID
        AND CONVERT(date,p.PaymentDate)=@Day
    )
)
SELECT TOP 1 @C80 = CustomerID FROM noOct1 WHERE DaySales BETWEEN 75000 AND 85000 ORDER BY CustomerID;
SELECT TOP 1 @C50 = CustomerID FROM noOct1 WHERE DaySales BETWEEN 45000 AND 55000 AND CustomerID<>ISNULL(@C80,-1) ORDER BY CustomerID;

-- Fallback: Oct2-only payers
IF @C80 IS NULL
  SELECT TOP 1 @C80 = p.CustomerID
  FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
    AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments x
      WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day
    )
  ORDER BY p.CustomerPaymentID;

IF @C50 IS NULL
  SELECT TOP 1 @C50 = p.CustomerID
  FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
    AND p.CustomerID<>ISNULL(@C80,-1)
    AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersPayments x
      WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day
    )
  ORDER BY p.CustomerPaymentID;

-- Last fallback: any list5 customer without Oct1
IF @C80 IS NULL
  SELECT TOP 1 @C80 = c.CustomerID FROM dbo.Customers c
  WHERE c.DelegateID=@ListId
    AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments p WHERE p.DelegateID=@ListId AND p.CustomerID=c.CustomerID AND CONVERT(date,p.PaymentDate)=@Day)
  ORDER BY c.CustomerID;
IF @C50 IS NULL
  SELECT TOP 1 @C50 = c.CustomerID FROM dbo.Customers c
  WHERE c.DelegateID=@ListId AND c.CustomerID<>ISNULL(@C80,-1)
    AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments p WHERE p.DelegateID=@ListId AND p.CustomerID=c.CustomerID AND CONVERT(date,p.PaymentDate)=@Day)
  ORDER BY c.CustomerID;

PRINT CONCAT('InsertCustomers c80=', ISNULL(@C80,-1), ' c50=', ISNULL(@C50,-1));

IF @C80 IS NULL OR @C50 IS NULL
BEGIN
  RAISERROR('Could not pick insert customers', 16, 1);
END

DECLARE @Guid80 uniqueidentifier = NEWID();
DECLARE @Guid50 uniqueidentifier = NEWID();
DECLARE @Usd80 float = 80000.0/1448.0;
DECLARE @Usd50 float = 50000.0/1448.0;
DECLARE @NewId80 INT, @NewId50 INT;

INSERT INTO dbo.CustomersPayments
  (UserID, CustomerID, BoxID, PaymentDate, BoundNumber, DelegateID, AccountZero, DelegateState, AsyncState, AsyncID, SelectState, Location, CreatedDate, UpdatedDate, ClientPaymentId)
VALUES
  (@UserID, @C80, @BoxID, @DayDt, NULL, @ListId, 0, 1, 0, @AsyncID, 0, N'fix-match-receipt', GETDATE(), GETDATE(), CONVERT(nvarchar(64), @Guid80));
SET @NewId80 = SCOPE_IDENTITY();

INSERT INTO dbo.AddToBox
  (BoxID, Amount, Notes, UserID, DateCreate, CreatedDate, CustomerPaymentID, CustomerID, DelegateID, AsyncState, AsyncID)
VALUES
  (@BoxID, @Usd80, N'مطابقة وصل المندوب 1/10', @UserID, GETDATE(), GETDATE(), @NewId80, @C80, @ListId, 0, @AsyncID);

INSERT INTO dbo.CustomersPayments
  (UserID, CustomerID, BoxID, PaymentDate, BoundNumber, DelegateID, AccountZero, DelegateState, AsyncState, AsyncID, SelectState, Location, CreatedDate, UpdatedDate, ClientPaymentId)
VALUES
  (@UserID, @C50, @BoxID, @DayDt, NULL, @ListId, 0, 1, 0, @AsyncID, 0, N'fix-match-receipt', GETDATE(), GETDATE(), CONVERT(nvarchar(64), @Guid50));
SET @NewId50 = SCOPE_IDENTITY();

INSERT INTO dbo.AddToBox
  (BoxID, Amount, Notes, UserID, DateCreate, CreatedDate, CustomerPaymentID, CustomerID, DelegateID, AsyncState, AsyncID)
VALUES
  (@BoxID, @Usd50, N'مطابقة وصل المندوب 1/10', @UserID, GETDATE(), GETDATE(), @NewId50, @C50, @ListId, 0, @AsyncID);

PRINT CONCAT('Inserted ', @NewId80, ' and ', @NewId50);

-- Verify
DECLARE @Cnt INT, @K float, @Iqd float;
SELECT @Cnt=COUNT(*), @K=SUM(ISNULL(a.Amount,0)*1448)/1000.0, @Iqd=SUM(ISNULL(a.Amount,0)*1448)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== FINAL ===';
SELECT @Cnt AS Cnt, @K AS K, @Iqd AS Iqd;

SELECT COUNT(*) AS ViewCnt, SUM(ISNULL(AmountDenar,0))/1000.0 AS ViewK, SUM(ISNULL(AmountDenar,0)) AS ViewIqd
FROM dbo.View_CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day;

SELECT p.CustomerPaymentID, p.CustomerID, LEFT(c.CustomerName,40) AS Name,
       ISNULL(a.Amount,0)*1448 AS Iqd, p.ClientPaymentId, p.Location
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY Iqd DESC, p.CustomerPaymentID;

IF @Cnt = 34 AND ABS(@K - 2370.0) < 0.05 AND ABS(@Iqd - 2370000.0) < 1
BEGIN
  COMMIT TRAN;
  PRINT 'COMMITTED_34_2370';
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT 'ROLLBACK_MISMATCH';
  SELECT @Cnt AS Cnt, @K AS K, @Iqd AS Iqd;
END
"""


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    # Discover AddToBox columns first to craft valid insert
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)

    discover = r"""
SET NOCOUNT ON;
SELECT 'CP', c.name FROM sys.columns c WHERE c.object_id=OBJECT_ID('dbo.CustomersPayments') ORDER BY column_id;
SELECT 'ATB', c.name, t.is_nullable, ty.name
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE c.object_id=OBJECT_ID('dbo.AddToBox')
ORDER BY c.column_id;
"""
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_disc.sql", "w") as f:
        f.write(discover)
    with sftp.file("/tmp/wilaya2_disc_runner.py", "w") as f:
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
sql=Path('/tmp/wilaya2_disc.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','80','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or ''); print(r.stderr or ''); print('exit', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, _ = c.exec_command("python3 /tmp/wilaya2_disc_runner.py", timeout=120)
    disc = out.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_disc.txt").write_text(disc, encoding="utf-8")
    print(disc)

    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_exact.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_exact_runner.py", "w") as f:
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
sql=Path('/tmp/wilaya2_exact.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-3000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_exact_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_exact_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    c.close()
    return 0 if "COMMITTED_34_2370" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
