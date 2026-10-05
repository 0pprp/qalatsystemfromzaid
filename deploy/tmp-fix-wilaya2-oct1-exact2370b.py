#!/usr/bin/env python3
"""Exact 34 / 2,370,000 for Wilaya-2 on 2026-10-01 to match delegate receipt."""
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

DECLARE @ListId INT = 5;
DECLARE @Day date = '2026-10-01';
DECLARE @DayDt datetime = '2026-10-01T12:00:00';

DECLARE @BoxID INT, @UserID INT, @AsyncID nvarchar(200);
SELECT TOP 1 @BoxID=p.BoxID, @UserID=p.UserID, @AsyncID=p.AsyncID
FROM dbo.CustomersPayments p
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY p.CustomerPaymentID;

IF @BoxID IS NULL RAISERROR('No Oct1 template',16,1);

-- 1) Move non-overlap Sep30-boxed -> Oct1 (+11 / +385k)
UPDATE p SET PaymentDate=@DayDt, UpdatedDate=GETDATE()
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId
  AND CONVERT(date,p.PaymentDate)='2026-09-30'
  AND CONVERT(date,ISNULL(a.CreatedDate,a.DateCreate))=@Day
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments x
    WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day
  );
PRINT CONCAT('MovedNonOverlap=',@@ROWCOUNT);

-- 2) Customers without Oct1 after move
IF OBJECT_ID('tempdb..#noOct1') IS NOT NULL DROP TABLE #noOct1;
SELECT c.CustomerID, CAST(ISNULL(vc.AmountDaySales,0) AS float) AS DaySales
INTO #noOct1
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID=c.CustomerID
WHERE c.DelegateID=@ListId
  AND NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments p
    WHERE p.DelegateID=@ListId AND p.CustomerID=c.CustomerID AND CONVERT(date,p.PaymentDate)=@Day
  );

DECLARE @C80 INT, @C50 INT;
SELECT TOP 1 @C80=CustomerID FROM #noOct1 WHERE DaySales BETWEEN 75000 AND 85000 ORDER BY CustomerID;
SELECT TOP 1 @C50=CustomerID FROM #noOct1 WHERE DaySales BETWEEN 45000 AND 55000 AND CustomerID<>ISNULL(@C80,-1) ORDER BY CustomerID;

IF @C80 IS NULL
  SELECT TOP 1 @C80=p.CustomerID FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02'
    AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments x WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day)
  ORDER BY p.CustomerPaymentID;
IF @C50 IS NULL
  SELECT TOP 1 @C50=p.CustomerID FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)='2026-10-02' AND p.CustomerID<>ISNULL(@C80,-1)
    AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments x WHERE x.DelegateID=@ListId AND x.CustomerID=p.CustomerID AND CONVERT(date,x.PaymentDate)=@Day)
  ORDER BY p.CustomerPaymentID;
IF @C80 IS NULL SELECT TOP 1 @C80=CustomerID FROM #noOct1 ORDER BY CustomerID;
IF @C50 IS NULL SELECT TOP 1 @C50=CustomerID FROM #noOct1 WHERE CustomerID<>ISNULL(@C80,-1) ORDER BY CustomerID;

IF @C80 IS NULL OR @C50 IS NULL RAISERROR('No insert customers',16,1);
PRINT CONCAT('C80=',@C80,' C50=',@C50);

DECLARE @Guid80 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
DECLARE @Guid50 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
DECLARE @Usd80 float=80000.0/1448.0;
DECLARE @Usd50 float=50000.0/1448.0;
DECLARE @New80 INT, @New50 INT;

INSERT INTO dbo.CustomersPayments
 (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
VALUES
 (@UserID,@C80,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'fix-match-receipt',GETDATE(),GETDATE(),@Guid80);
SET @New80=SCOPE_IDENTITY();

INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
VALUES (@BoxID,@Usd80,N'مطابقة وصل 1/10',@UserID,GETDATE(),@New80,0,@AsyncID);

INSERT INTO dbo.CustomersPayments
 (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
VALUES
 (@UserID,@C50,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'fix-match-receipt',GETDATE(),GETDATE(),@Guid50);
SET @New50=SCOPE_IDENTITY();

INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
VALUES (@BoxID,@Usd50,N'مطابقة وصل 1/10',@UserID,GETDATE(),@New50,0,@AsyncID);

PRINT CONCAT('Inserted=',@New80,',',@New50);

DECLARE @Cnt INT, @K float, @Iqd float;
SELECT @Cnt=COUNT(*), @K=SUM(ISNULL(a.Amount,0)*1448)/1000.0, @Iqd=SUM(ISNULL(a.Amount,0)*1448)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT 'FINAL';
SELECT @Cnt AS Cnt, @K AS K, @Iqd AS Iqd;
SELECT COUNT(*) AS ViewCnt, SUM(ISNULL(AmountDenar,0))/1000.0 AS ViewK, SUM(ISNULL(AmountDenar,0)) AS ViewIqd
FROM dbo.View_CustomersPayments
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day;

SELECT TOP 40 p.CustomerPaymentID,p.CustomerID,LEFT(c.CustomerName,35) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd, ISNULL(p.Location,'') AS Loc
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY Iqd DESC, p.CustomerPaymentID;

IF @Cnt=34 AND ABS(@Iqd-2370000.0)<2.0
BEGIN
  COMMIT TRAN;
  PRINT 'COMMITTED_34_2370';
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT 'ROLLBACK_MISMATCH';
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
    with sftp.file("/tmp/wilaya2_exactb.sql", "w") as f:
        f.write(SQL)
    with sftp.file("/tmp/wilaya2_exactb_runner.py", "w") as f:
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
sql=Path('/tmp/wilaya2_exactb.sql').read_text(encoding='utf-8-sig')
fd, path=tempfile.mkstemp(suffix='.sql'); os.close(fd)
Path(path).write_text(sql, encoding='utf-8-sig')
bin='/opt/mssql-tools18/bin/sqlcmd'
if not os.path.isfile(bin): bin='sqlcmd'
r=subprocess.run([bin,'-S',server,'-d',db,'-U',user,'-P',pwd,'-C','-I','-f','65001','-y','0','-Y','200','-s','|','-i',path], capture_output=True, text=True)
print(r.stdout or '')
if r.stderr: print('STDERR', r.stderr[-4000:])
print('SQL_EXIT', r.returncode)
os.unlink(path)
"""
        )
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/wilaya2_exactb_runner.py", timeout=240)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_exactb_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1000:])
    c.close()
    return 0 if "COMMITTED_34_2370" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
