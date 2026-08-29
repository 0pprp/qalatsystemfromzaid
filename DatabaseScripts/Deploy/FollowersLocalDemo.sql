/*
  Local Docker only: schema + two Nasiriyah lists + mixed paid/unpaid customers.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Delegates', 'AsyncID') IS NULL
    ALTER TABLE dbo.Delegates ADD AsyncID NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.Delegates', 'UserID') IS NULL
    ALTER TABLE dbo.Delegates ADD UserID INT NULL;
IF COL_LENGTH('dbo.Delegates', 'CityID') IS NULL
    ALTER TABLE dbo.Delegates ADD CityID INT NULL;
IF COL_LENGTH('dbo.Delegates', 'Address') IS NULL
    ALTER TABLE dbo.Delegates ADD Address NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.Delegates', 'PhoneNumber') IS NULL
    ALTER TABLE dbo.Delegates ADD PhoneNumber NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.Delegates', 'Notes') IS NULL
    ALTER TABLE dbo.Delegates ADD Notes NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Delegates', 'ReceiptName') IS NULL
    ALTER TABLE dbo.Delegates ADD ReceiptName NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.Delegates', 'DelegateState') IS NULL
    ALTER TABLE dbo.Delegates ADD DelegateState BIT NULL;
IF COL_LENGTH('dbo.Delegates', 'BoxID') IS NULL
    ALTER TABLE dbo.Delegates ADD BoxID INT NULL;
GO

UPDATE dbo.Delegates SET DelegateState = 1 WHERE DelegateState IS NULL;
GO

IF OBJECT_ID(N'dbo.SelectDelegate', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SelectDelegate (
        SelectDelegateID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DelegateFatherID INT NULL,
        DelegateChildID INT NULL,
        UserID INT NULL,
        AsyncState BIT NULL,
        AsyncID NVARCHAR(255) NULL,
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE()),
        UpdatedDate DATETIME NULL
    );
END
GO

IF OBJECT_ID(N'dbo.CustomersPayments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomersPayments (
        CustomerPaymentID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserID INT NULL,
        CustomerID INT NULL,
        BoxID INT NULL,
        PaymentDate DATETIME NULL,
        BoundNumber INT NULL,
        DelegateID INT NULL,
        AccountZero BIT NULL,
        DelegateState BIT NULL,
        AsyncState BIT NULL,
        AsyncID NVARCHAR(255) NULL,
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE())
    );
END
GO

IF OBJECT_ID(N'dbo.AddToBox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AddToBox (
        AddToBoxID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BoxID INT NULL,
        Amount FLOAT NULL,
        Notes NVARCHAR(MAX) NULL,
        UserID INT NULL,
        DateCreate DATETIME NULL,
        CustomerPaymentID INT NULL,
        AsyncState BIT NULL,
        AsyncID NVARCHAR(255) NULL,
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE())
    );
END
GO

IF OBJECT_ID(N'dbo.CompanyInformation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyInformation (
        CompanyInformationID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PhoneNumber NVARCHAR(255) NULL
    );
    INSERT INTO dbo.CompanyInformation (PhoneNumber) VALUES (N'07800000000');
END
GO

CREATE OR ALTER VIEW dbo.View_Delegates
AS
SELECT
    D.DelegateID,
    D.UserID,
    D.CityID,
    D.DelegateName,
    D.Address,
    D.PhoneNumber,
    D.Notes,
    CAST(NULL AS NVARCHAR(MAX)) AS DelegateImage,
    D.DelegateState,
    CAST(NULL AS FLOAT) AS ProfitRatio,
    CAST(0 AS BIT) AS SelectState,
    CAST(0 AS BIT) AS AsyncState,
    D.AsyncID,
    D.BoxID,
    CAST(NULL AS INT) AS BoxBalanceID,
    CAST(0 AS BIT) AS BalanceSaleState,
    CAST(0 AS BIT) AS DeviceSaleState,
    CAST(0 AS BIT) AS BalancePaymentState,
    CAST(0 AS BIT) AS DevicePaymentState,
    D.ReceiptName,
    CAST(0 AS BIT) AS UpdateReceipt,
    CAST(0 AS BIT) AS DeleteReceipt,
    (SELECT COUNT(*) FROM dbo.Customers C WHERE C.DelegateID = D.DelegateID) AS NumberOfCustomer,
    0 AS NumberOfCustomerIsLegal,
    0 AS NumberOfCustomerIsZero,
    0 AS NumberOfCustomerIsNotZero,
    0.0 AS AmountTotal,
    0.0 AS AmountCost,
    0.0 AS AmountDay,
    0.0 AS AmountRecever,
    0.0 AS AmountRemaining,
    N'الناصرية' AS CityName
FROM dbo.Delegates D;
GO

CREATE OR ALTER VIEW dbo.View_SelectDelegate
AS
SELECT
    SD.SelectDelegateID,
    SD.DelegateFatherID,
    SD.DelegateChildID,
    SD.UserID,
    SD.AsyncState,
    SD.DelegateChildID AS DelegateID,
    D.DelegateName,
    D.ReceiptName,
    CAST(0 AS BIT) AS UpdateReceipt,
    CAST(0 AS BIT) AS DeleteReceipt,
    CAST(0 AS BIT) AS DevicePaymentState
FROM dbo.SelectDelegate SD
INNER JOIN dbo.Delegates D ON D.DelegateID = SD.DelegateChildID;
GO

CREATE OR ALTER PROC dbo.Delegates_Create
    @DelegateName NVARCHAR(100),
    @UserCreateID INT,
    @Address NVARCHAR(100),
    @PhoneNumber NVARCHAR(100),
    @ReceiptName NVARCHAR(100),
    @AsyncID NVARCHAR(100),
    @Notes NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF @AsyncID IS NULL OR @AsyncID = N''
        SET @AsyncID = CONVERT(NVARCHAR(50), NEWID());

    INSERT INTO dbo.Boxes (BoxName, BoxState)
    VALUES (N'خزينة ' + @DelegateName, 1);

    DECLARE @BoxID INT = SCOPE_IDENTITY();

    INSERT INTO dbo.Delegates (DelegateName, UserID, Address, PhoneNumber, ReceiptName, AsyncID, Notes, DelegateState, BoxID)
    VALUES (@DelegateName, @UserCreateID, @Address, @PhoneNumber, @ReceiptName, @AsyncID, @Notes, 1, @BoxID);

    DECLARE @LastId INT = SCOPE_IDENTITY();
    SELECT * FROM dbo.View_Delegates WHERE DelegateID = @LastId;
END
GO

CREATE OR ALTER PROC dbo.Delegates_Update
    @DelegateID INT,
    @DelegateName NVARCHAR(100),
    @UserUpdateID INT,
    @Address NVARCHAR(100),
    @PhoneNumber NVARCHAR(100),
    @ReceiptName NVARCHAR(100),
    @AsyncID NVARCHAR(100) = NULL,
    @Notes NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF @AsyncID IS NULL OR @AsyncID = N''
        SELECT @AsyncID = AsyncID FROM dbo.Delegates WHERE DelegateID = @DelegateID;

    UPDATE dbo.Delegates
    SET DelegateName = @DelegateName,
        UserID = @UserUpdateID,
        Address = @Address,
        PhoneNumber = @PhoneNumber,
        AsyncID = @AsyncID,
        Notes = @Notes,
        ReceiptName = @ReceiptName
    WHERE DelegateID = @DelegateID;

    SELECT * FROM dbo.View_Delegates WHERE DelegateID = @DelegateID;
END
GO

CREATE OR ALTER PROC dbo.Delegates_Delete
    @DelegateID INT,
    @UserDeleteID INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Delegates SET DelegateState = 0 WHERE DelegateID = @DelegateID;
END
GO

CREATE OR ALTER PROC dbo.Delegates_GetAll
    @DelegateName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT *
    FROM dbo.View_Delegates
    WHERE DelegateState = 1
      AND (
          @DelegateName IS NULL
          OR @DelegateName = N'null'
          OR DelegateName LIKE N'%' + @DelegateName + N'%'
      );
END
GO

CREATE OR ALTER PROC dbo.Delegates_GetDataAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DelegateID, DelegateName, ReceiptName
    FROM dbo.Delegates
    WHERE DelegateState = 1;
END
GO

CREATE OR ALTER PROC dbo.SelectDelegate_Create
    @DelegateFatherID INT,
    @DelegateChildID INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.SelectDelegate
    WHERE DelegateFatherID = @DelegateFatherID AND DelegateChildID = @DelegateChildID;

    INSERT INTO dbo.SelectDelegate (DelegateFatherID, DelegateChildID)
    VALUES (@DelegateFatherID, @DelegateChildID);

    DECLARE @LastId INT = SCOPE_IDENTITY();
    SELECT * FROM dbo.View_SelectDelegate WHERE SelectDelegateID = @LastId;
END
GO

CREATE OR ALTER PROC dbo.SelectDelegate_Delete
    @SelectDelegateID INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.SelectDelegate WHERE SelectDelegateID = @SelectDelegateID;
END
GO

CREATE OR ALTER PROC dbo.SelectDelegate_GetByDelegateID
    @DelegateID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.View_SelectDelegate WHERE DelegateFatherID = @DelegateID;
END
GO

CREATE OR ALTER PROC dbo.GetDelegateLogin
    @AsyncID NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.View_Delegates WHERE AsyncID = @AsyncID;
END
GO

CREATE OR ALTER PROC dbo.GetDelegateSelect
    @DelegateID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.View_SelectDelegate WHERE DelegateFatherID = @DelegateID;
END
GO

CREATE OR ALTER PROC dbo.Followers_IsLinked
    @FatherID INT,
    @ChildID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.SelectDelegate
        WHERE DelegateFatherID = @FatherID AND DelegateChildID = @ChildID
    ) THEN 1 ELSE 0 END AS IsLinked;
END
GO

CREATE OR ALTER PROC dbo.Customers_Follow
    @DelegateID INT,
    @PaymentDate DATETIME,
    @ShowType NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @PaymentDateOnly DATE = CAST(@PaymentDate AS DATE);

    ;WITH Receipts AS
    (
        SELECT
            CP.CustomerID,
            ISNULL(SUM(ATB.Amount * 1448), 0) AS AmountReceipt
        FROM dbo.CustomersPayments CP
        INNER JOIN dbo.AddToBox ATB ON ATB.CustomerPaymentID = CP.CustomerPaymentID
        WHERE CAST(CP.PaymentDate AS DATE) = @PaymentDateOnly
        GROUP BY CP.CustomerID
    )
    SELECT
        C.CustomerID,
        C.DelegateID,
        C.UserID,
        C.CityID,
        C.CustomerName,
        C.Address,
        C.Longitude,
        C.Latitude,
        C.CustomerImage,
        C.Notes,
        C.PhoneNumber,
        C.CustomerState,
        C.ShopName,
        C.StoreAddress,
        C.NearestFunctionPoint,
        C.StorePhoneNumber,
        C.Neighborhood,
        C.AmountReceverDay,
        C.AsyncState,
        C.AsyncID,
        C.SelectState,
        C.SaleName,
        C.ReceiptName,
        C.IsLegal,
        C.IsFakeSale,
        N'الناصرية' AS CityName,
        ISNULL(D.DelegateName, N'') AS DelegateName,
        DATEADD(DAY, -10, GETDATE()) AS DateSaleDevice,
        1000000.0 AS AmountTotalSales,
        700000.0 AS CostTotalSales,
        20000.0 AS AmountDaySales,
        ISNULL(R.AmountReceipt, 0) AS ReceiptsTotal,
        1000000.0 - ISNULL(R.AmountReceipt, 0) AS AmountRemaining,
        N'موبايل تجريبي' AS ItemsNames,
        ISNULL(R.AmountReceipt, 0) AS AmountReceipt,
        1 AS CountReceiptDevice,
        10 AS NumberOfDayDevice,
        1 AS NumberOfDayPayment,
        DATEADD(DAY, -1, GETDATE()) AS LastPaymentDate
    FROM dbo.Customers C
    LEFT JOIN dbo.Delegates D ON C.DelegateID = D.DelegateID
    LEFT JOIN Receipts R ON C.CustomerID = R.CustomerID
    WHERE (@DelegateID = 0 OR C.DelegateID = @DelegateID)
      AND ISNULL(C.IsFakeSale, 0) = 0
      AND (
            (@ShowType = N'المسددين' AND ISNULL(R.AmountReceipt, 0) > 0)
         OR (@ShowType <> N'المسددين' AND ISNULL(R.AmountReceipt, 0) = 0)
      );
END
GO

DECLARE @UserID INT = (SELECT TOP 1 UserID FROM dbo.Users ORDER BY UserID);
DECLARE @Yesterday DATETIME = DATEADD(DAY, -1, CAST(GETDATE() AS DATE));
DECLARE @List1 INT, @List2 INT, @FollowerID INT, @Box1 INT, @Box2 INT;

IF NOT EXISTS (SELECT 1 FROM dbo.Delegates WHERE DelegateName = N'قائمة السوق')
BEGIN
    INSERT INTO dbo.Boxes (BoxName, BoxState) VALUES (N'خزينة قائمة السوق', 1);
    SET @Box1 = SCOPE_IDENTITY();
    INSERT INTO dbo.Delegates (DelegateName, UserID, Address, PhoneNumber, ReceiptName, AsyncID, Notes, DelegateState, BoxID)
    VALUES (N'قائمة السوق', @UserID, N'الناصرية', N'07811111111', N'جابي السوق', N'list1', N'قائمة تجريبية 1', 1, @Box1);
    SET @List1 = SCOPE_IDENTITY();
END
ELSE
    SELECT @List1 = DelegateID, @Box1 = BoxID FROM dbo.Delegates WHERE DelegateName = N'قائمة السوق';

IF NOT EXISTS (SELECT 1 FROM dbo.Delegates WHERE DelegateName = N'قائمة الفرات')
BEGIN
    INSERT INTO dbo.Boxes (BoxName, BoxState) VALUES (N'خزينة قائمة الفرات', 1);
    SET @Box2 = SCOPE_IDENTITY();
    INSERT INTO dbo.Delegates (DelegateName, UserID, Address, PhoneNumber, ReceiptName, AsyncID, Notes, DelegateState, BoxID)
    VALUES (N'قائمة الفرات', @UserID, N'الناصرية', N'07822222222', N'جابي الفرات', N'list2', N'قائمة تجريبية 2', 1, @Box2);
    SET @List2 = SCOPE_IDENTITY();
END
ELSE
    SELECT @List2 = DelegateID, @Box2 = BoxID FROM dbo.Delegates WHERE DelegateName = N'قائمة الفرات';

IF @Box1 IS NULL
    SELECT @Box1 = BoxID FROM dbo.Delegates WHERE DelegateID = @List1;
IF @Box2 IS NULL
    SELECT @Box2 = BoxID FROM dbo.Delegates WHERE DelegateID = @List2;

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerName = N'علي حسن' AND DelegateID = @List1)
BEGIN
    INSERT INTO dbo.Customers (DelegateID, UserID, CustomerName, Address, PhoneNumber, CustomerState, ShopName, IsLegal, IsFakeSale, AmountReceverDay)
    VALUES
        (@List1, @UserID, N'علي حسن', N'حي الصدر', N'07710000001', 1, N'محل علي', 0, 0, 20000),
        (@List1, @UserID, N'محمد جاسم', N'حي الحسين', N'07710000002', 1, N'محل محمد', 0, 0, 20000),
        (@List1, @UserID, N'حسين كاظم', N'الشموخ', N'07710000003', 1, N'محل حسين', 0, 0, 20000),
        (@List1, @UserID, N'سجاد عباس', N'الإصلاح', N'07710000004', 1, N'محل سجاد', 0, 0, 20000),
        (@List1, @UserID, N'أحمد فلاح', N'الجزائر', N'07710000005', 1, N'محل أحمد', 0, 0, 20000),
        (@List2, @UserID, N'أمين رحيم', N'السوق', N'07720000001', 1, N'محل أمين', 0, 0, 15000),
        (@List2, @UserID, N'كرار مهدي', N'اليرموك', N'07720000002', 1, N'محل كرار', 0, 0, 15000),
        (@List2, @UserID, N'زهراء علي', N'الحبوبي', N'07720000003', 1, N'محل زهراء', 0, 0, 15000),
        (@List2, @UserID, N'مصطفى نوري', N'الأمل', N'07720000004', 1, N'محل مصطفى', 0, 0, 15000),
        (@List2, @UserID, N'حيدر سالم', N'المنصور', N'07720000005', 1, N'محل حيدر', 0, 0, 15000);
END

DECLARE @Paid TABLE (CustomerName NVARCHAR(100), DelegateID INT);
INSERT INTO @Paid VALUES
    (N'علي حسن', @List1),
    (N'محمد جاسم', @List1),
    (N'أحمد فلاح', @List1),
    (N'أمين رحيم', @List2),
    (N'مصطفى نوري', @List2);

INSERT INTO dbo.CustomersPayments (UserID, CustomerID, BoxID, PaymentDate, BoundNumber, DelegateID, AccountZero, DelegateState, AsyncState, AsyncID)
SELECT @UserID, C.CustomerID,
       CASE WHEN C.DelegateID = @List1 THEN @Box1 ELSE @Box2 END,
       @Yesterday, 1, C.DelegateID, 0, 1, 0, CONVERT(NVARCHAR(50), NEWID())
FROM dbo.Customers C
INNER JOIN @Paid P ON P.CustomerName = C.CustomerName AND P.DelegateID = C.DelegateID
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.CustomersPayments CP
    WHERE CP.CustomerID = C.CustomerID AND CAST(CP.PaymentDate AS DATE) = CAST(@Yesterday AS DATE)
);

INSERT INTO dbo.AddToBox (BoxID, Amount, Notes, UserID, DateCreate, CustomerPaymentID, AsyncState, AsyncID)
SELECT CP.BoxID, 20, N'قبض تجريبي من ' + C.CustomerName, @UserID, @Yesterday, CP.CustomerPaymentID, 0, CONVERT(NVARCHAR(50), NEWID())
FROM dbo.CustomersPayments CP
INNER JOIN dbo.Customers C ON C.CustomerID = CP.CustomerID
INNER JOIN @Paid P ON P.CustomerName = C.CustomerName AND P.DelegateID = C.DelegateID
WHERE CAST(CP.PaymentDate AS DATE) = CAST(@Yesterday AS DATE)
  AND NOT EXISTS (SELECT 1 FROM dbo.AddToBox A WHERE A.CustomerPaymentID = CP.CustomerPaymentID);

IF NOT EXISTS (SELECT 1 FROM dbo.Delegates WHERE DelegateName = N'متابع الناصرية')
BEGIN
    INSERT INTO dbo.Boxes (BoxName, BoxState) VALUES (N'خزينة متابع الناصرية', 1);
    INSERT INTO dbo.Delegates (DelegateName, UserID, Address, PhoneNumber, ReceiptName, AsyncID, Notes, DelegateState, BoxID)
    VALUES (N'متابع الناصرية', @UserID, N'الناصرية', N'07833333333', N'المتابع', N'follower1', N'حساب المتابع للتجربة', 1, SCOPE_IDENTITY());
    SET @FollowerID = SCOPE_IDENTITY();
END
ELSE
    SELECT @FollowerID = DelegateID FROM dbo.Delegates WHERE DelegateName = N'متابع الناصرية';

IF @FollowerID IS NOT NULL AND @List1 IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.SelectDelegate WHERE DelegateFatherID = @FollowerID AND DelegateChildID = @List1)
    INSERT INTO dbo.SelectDelegate (DelegateFatherID, DelegateChildID) VALUES (@FollowerID, @List1);

IF @FollowerID IS NOT NULL AND @List2 IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.SelectDelegate WHERE DelegateFatherID = @FollowerID AND DelegateChildID = @List2)
    INSERT INTO dbo.SelectDelegate (DelegateFatherID, DelegateChildID) VALUES (@FollowerID, @List2);

PRINT N'Followers local demo ready';
GO
