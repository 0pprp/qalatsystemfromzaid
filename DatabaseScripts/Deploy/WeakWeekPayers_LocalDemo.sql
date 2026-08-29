/*
  Local demo seed for Docker DatabaseCompany.
  Creates a minimal Customers table + week view so the decision board can load.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers (
        CustomerID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DelegateID INT NULL,
        UserID INT NULL,
        CityID INT NULL,
        CustomerName NVARCHAR(255) NULL,
        Address NVARCHAR(255) NULL,
        Longitude FLOAT NULL,
        Latitude FLOAT NULL,
        CustomerImage NVARCHAR(MAX) NULL,
        Notes NVARCHAR(MAX) NULL,
        PhoneNumber NVARCHAR(255) NULL,
        CustomerState BIT NULL DEFAULT (1),
        ShopName NVARCHAR(255) NULL,
        StoreAddress NVARCHAR(255) NULL,
        NearestFunctionPoint NVARCHAR(255) NULL,
        StorePhoneNumber NVARCHAR(255) NULL,
        Neighborhood NVARCHAR(255) NULL,
        AmountReceverDay FLOAT NULL,
        AsyncState BIT NULL,
        AsyncID NVARCHAR(255) NULL,
        SelectState BIT NULL DEFAULT (0),
        SaleName NVARCHAR(255) NULL,
        ReceiptName NVARCHAR(255) NULL,
        IsLegal BIT NOT NULL DEFAULT (0),
        IsFakeSale BIT NOT NULL DEFAULT (0),
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE()),
        UpdatedDate DATETIME NULL
    );
END
GO

IF COL_LENGTH('dbo.Customers', 'IsFakeSale') IS NULL
    ALTER TABLE dbo.Customers ADD IsFakeSale BIT NOT NULL CONSTRAINT DF_Customers_IsFakeSale_Local DEFAULT (0);
GO

IF OBJECT_ID(N'dbo.Delegates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Delegates (
        DelegateID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DelegateName NVARCHAR(255) NULL
    );
    INSERT INTO dbo.Delegates (DelegateName) VALUES (N'مندوب تجريبي');
END
GO

IF OBJECT_ID(N'dbo.CustomerWeekPaymentSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWeekPaymentSnapshot (
        CustomerID INT NOT NULL PRIMARY KEY,
        DelegateID INT NULL,
        UserID INT NULL,
        CityID INT NULL,
        CustomerName NVARCHAR(255) NULL,
        Address NVARCHAR(255) NULL,
        Longitude FLOAT NULL,
        Latitude FLOAT NULL,
        CustomerImage NVARCHAR(MAX) NULL,
        Notes NVARCHAR(MAX) NULL,
        PhoneNumber NVARCHAR(255) NULL,
        CustomerState BIT NULL,
        ShopName NVARCHAR(255) NULL,
        StoreAddress NVARCHAR(255) NULL,
        NearestFunctionPoint NVARCHAR(255) NULL,
        StorePhoneNumber NVARCHAR(255) NULL,
        Neighborhood NVARCHAR(255) NULL,
        AmountReceverDay FLOAT NULL,
        AsyncState BIT NULL,
        AsyncID NVARCHAR(255) NULL,
        SelectState BIT NULL,
        SaleName NVARCHAR(255) NULL,
        ReceiptName NVARCHAR(255) NULL,
        IsLegal BIT NULL,
        IsFakeSale BIT NULL,
        CreatedDate DATETIME NULL,
        UpdatedDate DATETIME NULL,
        DelegateName NVARCHAR(255) NULL,
        UserName NVARCHAR(255) NULL,
        CityName NVARCHAR(255) NULL,
        DateSaleDevice DATETIME NULL,
        AmountTotalSales FLOAT NULL,
        CostTotalSales FLOAT NULL,
        AmountDaySales FLOAT NULL,
        ReceiptsTotal FLOAT NULL,
        AmountRemaining FLOAT NULL,
        ReceiptRateDevice FLOAT NULL,
        NumberOfDayDevice INT NULL,
        PhoneNumberCompany NVARCHAR(255) NULL,
        CountReceiptDevice INT NULL,
        ItemsNames NVARCHAR(MAX) NULL,
        LastPaymentDate DATETIME NULL,
        NumberOfDayPayment INT NULL,
        Amount1 FLOAT NULL,
        Amount2 FLOAT NULL,
        Amount3 FLOAT NULL,
        Amount4 FLOAT NULL,
        Amount5 FLOAT NULL,
        Amount6 FLOAT NULL,
        Amount7 FLOAT NULL
    );
END
GO

CREATE OR ALTER VIEW [dbo].[View_CustomerWeekPaymentDevice]
AS
SELECT *
FROM dbo.CustomerWeekPaymentSnapshot;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerName = N'زبون ضعيف صفر')
BEGIN
    INSERT INTO dbo.Customers (DelegateID, CustomerName, PhoneNumber, CustomerState, ShopName, IsLegal, IsFakeSale)
    VALUES
        (1, N'زبون ضعيف صفر', N'07700000001', 1, N'محل تجريبي 1', 0, 0),
        (1, N'زبون ضعيف 1٪', N'07700000002', 1, N'محل تجريبي 2', 0, 0),
        (1, N'زبون ضعيف 2٪', N'07700000003', 1, N'محل تجريبي 3', 0, 0),
        (1, N'زبون مسدد 5٪', N'07700000004', 1, N'محل تجريبي 4', 0, 0);
END
GO

DELETE FROM dbo.CustomerWeekPaymentSnapshot;

INSERT INTO dbo.CustomerWeekPaymentSnapshot
    (CustomerID, DelegateID, CustomerName, PhoneNumber, CustomerState, ShopName,
     IsLegal, IsFakeSale, DelegateName, UserName, CityName,
     DateSaleDevice, AmountTotalSales, CostTotalSales, AmountDaySales,
     ReceiptsTotal, AmountRemaining, ReceiptRateDevice, NumberOfDayDevice,
     CountReceiptDevice, ItemsNames, LastPaymentDate, NumberOfDayPayment,
     Amount1, Amount2, Amount3, Amount4, Amount5, Amount6, Amount7)
SELECT
    C.CustomerID, C.DelegateID, C.CustomerName, C.PhoneNumber, C.CustomerState, C.ShopName,
    C.IsLegal, C.IsFakeSale, N'مندوب تجريبي', N'احمد', N'الناصرية',
    DATEADD(DAY, -20, GETDATE()),
    1000000, 700000, 20000,
    CASE C.CustomerName
        WHEN N'زبون ضعيف صفر' THEN 0
        WHEN N'زبون ضعيف 1٪' THEN 10000
        WHEN N'زبون ضعيف 2٪' THEN 20000
        ELSE 50000
    END,
    CASE C.CustomerName
        WHEN N'زبون ضعيف صفر' THEN 1000000
        WHEN N'زبون ضعيف 1٪' THEN 990000
        WHEN N'زبون ضعيف 2٪' THEN 980000
        ELSE 950000
    END,
    0, 20, 2, N'موبايل تجريبي', DATEADD(DAY, -3, GETDATE()), 3,
    CASE C.CustomerName
        WHEN N'زبون ضعيف صفر' THEN 0
        WHEN N'زبون ضعيف 1٪' THEN 10000
        WHEN N'زبون ضعيف 2٪' THEN 20000
        ELSE 50000
    END,
    0, 0, 0, 0, 0, 0
FROM dbo.Customers C
WHERE C.CustomerName IN (N'زبون ضعيف صفر', N'زبون ضعيف 1٪', N'زبون ضعيف 2٪', N'زبون مسدد 5٪');
GO

CREATE OR ALTER PROC [dbo].[Customers_InfoSimple]
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'dbo.View_CustomersInfoSimple', N'V') IS NOT NULL
    BEGIN
        SELECT * FROM View_CustomersInfoSimple WHERE CustomerID = @CustomerID;
        RETURN;
    END

    SELECT
        CustomerID,
        DelegateID,
        CustomerName,
        DelegateName,
        AmountDaySales,
        ReceiptsTotal,
        AmountTotalSales,
        AmountRemaining
    FROM View_CustomerWeekPaymentDevice
    WHERE CustomerID = @CustomerID;
END
GO

PRINT N'Local demo seed done';
GO
