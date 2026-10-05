#!/usr/bin/env python3
"""
Apply corrected Wilaya-2 Oct1 sheet:
- YELLOW + payId: keep
- YELLOW no payId: insert by customer name
- RED: update amount
- Oct1 payments not in sheet: move off day (delete from 1/10)
"""
from __future__ import annotations

import os
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
LIST_ID = 5
DAY = "2026-10-01"

KEEP_IDS = [
    418687, 418575, 418738, 418739, 418744, 418689, 418680, 418571, 418681,
    418566, 418570, 418686, 418688, 418682, 418684, 418692, 418740, 418690,
    418743, 418567, 418685, 418691, 418693, 418574, 418573, 418568, 418694,
    418742, 418577, 418741,
]

RED_UPDATES = {
    418577: 15000,  # مهدي باقر عبد الرسول
}

NEW_PAYMENTS = [
    ("محمد يحيى ناجي 6", 25000),
    ("حيدر حسن مطرود", 30000),
    ("محمد كريم عبد 2", 10000),
    ("صباح حسن عودة 8", 15000),
    ("احمد مهدي احمد", 75000),
    ("مهند مهدي باقر 2", 10000),
]


def sql_escape(s: str) -> str:
    return s.replace("'", "''")


def build_sql() -> str:
    keep_list = ",".join(str(i) for i in KEEP_IDS)
    parts: list[str] = [
        f"""
SET NOCOUNT ON;
SET LANGUAGE us_english;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @ListId INT = {LIST_ID};
DECLARE @Day date = '{DAY}';
DECLARE @DayDt datetime = '{DAY}T12:00:00';

DECLARE @BoxID INT, @UserID INT, @AsyncID nvarchar(200);
SELECT TOP 1 @BoxID=p.BoxID, @UserID=p.UserID, @AsyncID=p.AsyncID
FROM dbo.CustomersPayments p
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY p.CustomerPaymentID;

IF @BoxID IS NULL
BEGIN
  SELECT TOP 1 @BoxID=p.BoxID, @UserID=p.UserID, @AsyncID=p.AsyncID
  FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId
  ORDER BY p.CustomerPaymentID DESC;
END
IF @BoxID IS NULL RAISERROR('No BoxID template',16,1);

PRINT '=== BEFORE ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

PRINT '=== TO DELETE (Oct1 not in sheet) ===';
SELECT p.CustomerPaymentID, p.CustomerID, LEFT(c.CustomerName,40) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
  AND p.CustomerPaymentID NOT IN ({keep_list})
ORDER BY Iqd DESC;
"""
    ]

    for pay_id, iqd in RED_UPDATES.items():
        parts.append(
            f"""
PRINT 'RED {pay_id} -> {iqd}';
UPDATE dbo.AddToBox
SET Amount = CONVERT(float, CONVERT(decimal(18,8), {iqd}) / CONVERT(decimal(18,8), 1448)),
    Notes = N'تعديل حسب جدول صحيح 1/10'
WHERE CustomerPaymentID = {pay_id};
SELECT {pay_id} AS PayId, CAST(ISNULL(Amount,0)*1448 AS bigint) AS IqdNow
FROM dbo.AddToBox WHERE CustomerPaymentID={pay_id};
"""
        )

    parts.append(
        f"""
PRINT 'MOVE off Oct1 (not in sheet)';
UPDATE dbo.CustomersPayments
SET PaymentDate = '2026-09-29T12:00:00',
    UpdatedDate = GETDATE(),
    Location = N'removed-not-in-correct-sheet-1-10'
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day
  AND CustomerPaymentID NOT IN ({keep_list});
PRINT CONCAT('MovedOff=', @@ROWCOUNT);
"""
    )

    for idx, (name, iqd) in enumerate(NEW_PAYMENTS):
        n = sql_escape(name.strip())
        tag = f"n{idx}"
        parts.append(
            f"""
PRINT 'NEW {tag} {n} {iqd}';
DECLARE @Cid_{tag} INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'{n}'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'{n}'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'{n}%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'{n}' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'{n}%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_{tag} IS NULL
  PRINT 'MISSING_CUSTOMER {n}';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_{tag}
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_{tag});
ELSE
BEGIN
  DECLARE @Guid_{tag} nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_{tag} float = CONVERT(float, CONVERT(decimal(18,8), {iqd}) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_{tag} INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_{tag},@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_{tag});
  SET @NewPay_{tag}=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_{tag},N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_{tag},0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_{tag},' cust=',@Cid_{tag},' iqd={iqd}');
END
"""
        )

    parts.append(
        """
PRINT '=== AFTER ===';
SELECT COUNT(*) AS Cnt, CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

SELECT p.CustomerPaymentID, p.CustomerID, LEFT(c.CustomerName,40) AS Name,
       CAST(ISNULL(a.Amount,0)*1448 AS bigint) AS Iqd
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
LEFT JOIN dbo.Customers c ON c.CustomerID=p.CustomerID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day
ORDER BY Iqd DESC, p.CustomerPaymentID;

DECLARE @FinalCnt INT, @FinalIqd bigint;
SELECT @FinalCnt=COUNT(*), @FinalIqd=CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint)
FROM dbo.CustomersPayments p
JOIN dbo.AddToBox a ON a.CustomerPaymentID=p.CustomerPaymentID
WHERE p.DelegateID=@ListId AND CONVERT(date,p.PaymentDate)=@Day;

IF @FinalCnt = 36
BEGIN
  COMMIT TRAN;
  PRINT CONCAT('COMMITTED_OK cnt=',@FinalCnt,' iqd=',@FinalIqd);
END
ELSE
BEGIN
  ROLLBACK TRAN;
  PRINT CONCAT('ROLLBACK_CNT cnt=',@FinalCnt,' iqd=',@FinalIqd,' expected=36');
END
"""
    )
    return "\n".join(parts)


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    sql = build_sql()
    Path(__file__).with_name("_wilaya2_correct_apply.sql").write_text(sql, encoding="utf-8")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    sftp = c.open_sftp()
    with sftp.file("/tmp/wilaya2_correct_apply.sql", "w") as f:
        f.write(sql)
    with sftp.file("/tmp/wilaya2_correct_apply_runner.py", "w") as f:
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
sql=Path('/tmp/wilaya2_correct_apply.sql').read_text(encoding='utf-8-sig')
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
    _, out, err = c.exec_command("python3 /tmp/wilaya2_correct_apply_runner.py", timeout=300)
    text = out.read().decode("utf-8", "replace")
    e = err.read().decode("utf-8", "replace")
    Path(__file__).with_name("_wilaya2_correct_apply_out.txt").write_text(
        text + (("\nSSH_ERR\n" + e) if e.strip() else ""), encoding="utf-8"
    )
    print(text)
    if e.strip():
        print("SSH_ERR", e[-1000:])
    c.close()
    return 0 if "COMMITTED_OK" in text else 1


if __name__ == "__main__":
    raise SystemExit(main())
