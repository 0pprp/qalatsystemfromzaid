
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
  AND p.CustomerPaymentID NOT IN (418687,418575,418738,418739,418744,418689,418680,418571,418681,418566,418570,418686,418688,418682,418684,418692,418740,418690,418743,418567,418685,418691,418693,418574,418573,418568,418694,418742,418577,418741)
ORDER BY Iqd DESC;


PRINT 'RED 418577 -> 15000';
UPDATE dbo.AddToBox
SET Amount = CONVERT(float, CONVERT(decimal(18,8), 15000) / CONVERT(decimal(18,8), 1448)),
    Notes = N'تعديل حسب جدول صحيح 1/10'
WHERE CustomerPaymentID = 418577;
SELECT 418577 AS PayId, CAST(ISNULL(Amount,0)*1448 AS bigint) AS IqdNow
FROM dbo.AddToBox WHERE CustomerPaymentID=418577;


PRINT 'MOVE off Oct1 (not in sheet)';
UPDATE dbo.CustomersPayments
SET PaymentDate = '2026-09-29T12:00:00',
    UpdatedDate = GETDATE(),
    Location = N'removed-not-in-correct-sheet-1-10'
WHERE DelegateID=@ListId AND CONVERT(date,PaymentDate)=@Day
  AND CustomerPaymentID NOT IN (418687,418575,418738,418739,418744,418689,418680,418571,418681,418566,418570,418686,418688,418682,418684,418692,418740,418690,418743,418567,418685,418691,418693,418574,418573,418568,418694,418742,418577,418741);
PRINT CONCAT('MovedOff=', @@ROWCOUNT);


PRINT 'NEW n0 محمد يحيى ناجي 6 25000';
DECLARE @Cid_n0 INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'محمد يحيى ناجي 6'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'محمد يحيى ناجي 6'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'محمد يحيى ناجي 6%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'محمد يحيى ناجي 6' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'محمد يحيى ناجي 6%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_n0 IS NULL
  PRINT 'MISSING_CUSTOMER محمد يحيى ناجي 6';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_n0
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_n0);
ELSE
BEGIN
  DECLARE @Guid_n0 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_n0 float = CONVERT(float, CONVERT(decimal(18,8), 25000) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_n0 INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_n0,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_n0);
  SET @NewPay_n0=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_n0,N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_n0,0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_n0,' cust=',@Cid_n0,' iqd=25000');
END


PRINT 'NEW n1 حيدر حسن مطرود 30000';
DECLARE @Cid_n1 INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'حيدر حسن مطرود'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'حيدر حسن مطرود'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'حيدر حسن مطرود%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'حيدر حسن مطرود' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'حيدر حسن مطرود%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_n1 IS NULL
  PRINT 'MISSING_CUSTOMER حيدر حسن مطرود';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_n1
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_n1);
ELSE
BEGIN
  DECLARE @Guid_n1 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_n1 float = CONVERT(float, CONVERT(decimal(18,8), 30000) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_n1 INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_n1,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_n1);
  SET @NewPay_n1=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_n1,N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_n1,0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_n1,' cust=',@Cid_n1,' iqd=30000');
END


PRINT 'NEW n2 محمد كريم عبد 2 10000';
DECLARE @Cid_n2 INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'محمد كريم عبد 2'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'محمد كريم عبد 2'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'محمد كريم عبد 2%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'محمد كريم عبد 2' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'محمد كريم عبد 2%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_n2 IS NULL
  PRINT 'MISSING_CUSTOMER محمد كريم عبد 2';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_n2
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_n2);
ELSE
BEGIN
  DECLARE @Guid_n2 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_n2 float = CONVERT(float, CONVERT(decimal(18,8), 10000) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_n2 INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_n2,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_n2);
  SET @NewPay_n2=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_n2,N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_n2,0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_n2,' cust=',@Cid_n2,' iqd=10000');
END


PRINT 'NEW n3 صباح حسن عودة 8 15000';
DECLARE @Cid_n3 INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'صباح حسن عودة 8'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'صباح حسن عودة 8'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'صباح حسن عودة 8%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'صباح حسن عودة 8' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'صباح حسن عودة 8%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_n3 IS NULL
  PRINT 'MISSING_CUSTOMER صباح حسن عودة 8';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_n3
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_n3);
ELSE
BEGIN
  DECLARE @Guid_n3 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_n3 float = CONVERT(float, CONVERT(decimal(18,8), 15000) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_n3 INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_n3,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_n3);
  SET @NewPay_n3=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_n3,N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_n3,0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_n3,' cust=',@Cid_n3,' iqd=15000');
END


PRINT 'NEW n4 احمد مهدي احمد 75000';
DECLARE @Cid_n4 INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'احمد مهدي احمد'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'احمد مهدي احمد'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'احمد مهدي احمد%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'احمد مهدي احمد' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'احمد مهدي احمد%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_n4 IS NULL
  PRINT 'MISSING_CUSTOMER احمد مهدي احمد';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_n4
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_n4);
ELSE
BEGIN
  DECLARE @Guid_n4 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_n4 float = CONVERT(float, CONVERT(decimal(18,8), 75000) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_n4 INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_n4,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_n4);
  SET @NewPay_n4=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_n4,N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_n4,0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_n4,' cust=',@Cid_n4,' iqd=75000');
END


PRINT 'NEW n5 مهند مهدي باقر 2 10000';
DECLARE @Cid_n5 INT =
(
  SELECT TOP 1 c.CustomerID
  FROM dbo.Customers c
  WHERE c.DelegateID = @ListId
    AND (
      LTRIM(RTRIM(c.CustomerName)) = N'مهند مهدي باقر 2'
      OR REPLACE(LTRIM(RTRIM(c.CustomerName)), N'  ', N' ') = N'مهند مهدي باقر 2'
      OR LTRIM(RTRIM(c.CustomerName)) LIKE N'مهند مهدي باقر 2%'
    )
  ORDER BY
    CASE WHEN LTRIM(RTRIM(c.CustomerName)) = N'مهند مهدي باقر 2' THEN 0
         WHEN LTRIM(RTRIM(c.CustomerName)) LIKE N'مهند مهدي باقر 2%' THEN 1
         ELSE 2 END,
    c.CustomerID
);
IF @Cid_n5 IS NULL
  PRINT 'MISSING_CUSTOMER مهند مهدي باقر 2';
ELSE IF EXISTS (
  SELECT 1 FROM dbo.CustomersPayments p
  WHERE p.DelegateID=@ListId AND p.CustomerID=@Cid_n5
    AND CONVERT(date,p.PaymentDate)=@Day
)
  PRINT CONCAT('ALREADY_EXISTS cust=', @Cid_n5);
ELSE
BEGIN
  DECLARE @Guid_n5 nvarchar(64)=CONVERT(nvarchar(64),NEWID());
  DECLARE @Usd_n5 float = CONVERT(float, CONVERT(decimal(18,8), 10000) / CONVERT(decimal(18,8), 1448));
  DECLARE @NewPay_n5 INT;
  INSERT INTO dbo.CustomersPayments
   (UserID,CustomerID,BoxID,PaymentDate,DelegateID,AccountZero,DelegateState,AsyncState,AsyncID,SelectState,Location,CreatedDate,UpdatedDate,ClientPaymentId)
  VALUES
   (@UserID,@Cid_n5,@BoxID,@DayDt,@ListId,0,1,0,@AsyncID,0,N'sheet-correct-1-10',GETDATE(),GETDATE(),@Guid_n5);
  SET @NewPay_n5=SCOPE_IDENTITY();
  INSERT INTO dbo.AddToBox (BoxID,Amount,Notes,UserID,DateCreate,CustomerPaymentID,AsyncState,AsyncID)
  VALUES (@BoxID,@Usd_n5,N'إضافة حسب جدول صحيح 1/10',@UserID,GETDATE(),@NewPay_n5,0,@AsyncID);
  PRINT CONCAT('INSERTED pay=',@NewPay_n5,' cust=',@Cid_n5,' iqd=10000');
END


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
