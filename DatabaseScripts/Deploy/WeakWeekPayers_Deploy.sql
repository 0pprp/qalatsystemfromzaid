/*
  WeakWeekPayers_Deploy.sql
  Apply this script on EVERY province database (each branch DB) before going live.

  Example (sqlcmd):
    sqlcmd -S YOUR_SQL_HOST -d DatabaseCompanyNajaf -E -I -i WeakWeekPayers_Deploy.sql
    sqlcmd -S YOUR_SQL_HOST -d DatabaseCompanyBaghdad -E -I -i WeakWeekPayers_Deploy.sql

  Repeat for each DatabaseCompany* used by GetAdmin / GetEmployee.

  After deploy:
    - Publish/restart BE_Company
    - Deploy FE_Company
    - Create a user with UserType = N'مدير فرع' from المستخدمين (محاسب رئيسي only)
    - That user logs in on FE_Company for that province only
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Customers', 'IsFakeSale') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD IsFakeSale BIT NOT NULL CONSTRAINT DF_Customers_IsFakeSale DEFAULT (0);
END
GO

CREATE OR ALTER VIEW [dbo].[View_CustomerWeekPaymentDevice]
AS
WITH SalesData AS (
    SELECT
        CustomerID,
        ROUND(SUM(ISNULL(AmountTotalSalesDenar, 0)), -3) AS AmountTotalSales,
        ROUND(SUM(ISNULL(AmountTotalCostDenar, 0)), -3) AS CostTotalSales,
        ROUND(SUM(ISNULL(AmountDaySalesDenar, 0)), -3) AS AmountDaySales,
        MAX(DateCreate) AS DateSaleDevice
    FROM View_CustomersSalesDelegate
    GROUP BY CustomerID
),
PaymentData AS (
    SELECT
        CustomerID,
        ROUND(SUM(ISNULL(AmountDenar, 0)), -3) AS ReceiptsTotal,
        MAX(PaymentDate) AS LastPaymentDate,
        COUNT(CustomerPaymentID) AS CountReceiptDevice
    FROM View_CustomersPaymentsDelegate
    GROUP BY CustomerID
),
AddToBoxData AS (
    SELECT
        CustomerIDPayment AS CustomerID,
        SUM(ISNULL(Amount, 0)) AS TotalAmount
    FROM View_AddToBox
    GROUP BY CustomerIDPayment
),
ItemsData AS (
    SELECT CustomerID, ItemsNames
    FROM View_CustomersTempItemsNames
)
SELECT
    C.*,
    MAX(ISNULL(D.DelegateName, '')) AS DelegateName,
    MAX(ISNULL(U.UserName, '')) AS UserName,
    MAX(ISNULL(CI.CityName, '')) AS CityName,
    MAX(SD.DateSaleDevice) AS DateSaleDevice,
    MAX(SD.AmountTotalSales) AS AmountTotalSales,
    MAX(SD.CostTotalSales) AS CostTotalSales,
    MAX(SD.AmountDaySales) AS AmountDaySales,
    MAX(PD.ReceiptsTotal) AS ReceiptsTotal,
    ROUND(MAX(SD.AmountTotalSales) - MAX(PD.ReceiptsTotal), -3) AS AmountRemaining,
    FLOOR(
        CASE
            WHEN ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)) <> 0
            THEN ((MAX(ATB.TotalAmount) * 100.0) / ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)))
            ELSE 0
        END
    ) AS ReceiptRateDevice,
    ISNULL(
        DATEDIFF(DAY,
            MAX(SD.DateSaleDevice),
            CASE
                WHEN (MAX(SD.AmountTotalSales) - MAX(PD.ReceiptsTotal)) <= 0 AND MAX(PD.LastPaymentDate) IS NOT NULL
                THEN MAX(PD.LastPaymentDate)
                ELSE GETDATE()
            END
        ) + 1, 0
    ) AS NumberOfDayDevice,
    (SELECT TOP 1 PhoneNumber FROM CompanyInformation) AS PhoneNumberCompany,
    MAX(PD.CountReceiptDevice) + 1 AS CountReceiptDevice,
    MAX(ISNULL(ID.ItemsNames, '')) AS ItemsNames,
    MAX(PD.LastPaymentDate) AS LastPaymentDate,
    ISNULL(DATEDIFF(DAY, MAX(PD.LastPaymentDate), GETDATE()) + 1, 0) AS NumberOfDayPayment,
    MAX(ISNULL(WF.Amount1, 0)) AS Amount1,
    MAX(ISNULL(WF.Amount2, 0)) AS Amount2,
    MAX(ISNULL(WF.Amount3, 0)) AS Amount3,
    MAX(ISNULL(WF.Amount4, 0)) AS Amount4,
    MAX(ISNULL(WF.Amount5, 0)) AS Amount5,
    MAX(ISNULL(WF.Amount6, 0)) AS Amount6,
    MAX(ISNULL(WF.Amount7, 0)) AS Amount7
FROM dbo.Customers C
LEFT JOIN Delegates D ON C.DelegateID = D.DelegateID
LEFT JOIN Users U ON C.UserID = U.UserID
LEFT JOIN Cities CI ON C.CityID = CI.CityID
LEFT JOIN SalesData SD ON C.CustomerID = SD.CustomerID
LEFT JOIN PaymentData PD ON C.CustomerID = PD.CustomerID
LEFT JOIN AddToBoxData ATB ON C.CustomerID = ATB.CustomerID
LEFT JOIN ItemsData ID ON C.CustomerID = ID.CustomerID
LEFT JOIN View_WeekFinal WF ON C.CustomerID = WF.CustomerID
GROUP BY
    C.CustomerID, C.DelegateID, C.UserID, C.CityID, C.CustomerName, C.Address,
    C.Longitude, C.Latitude, C.Notes, C.PhoneNumber, C.CustomerState, C.ShopName,
    C.StoreAddress, C.NearestFunctionPoint, C.StorePhoneNumber, C.Neighborhood,
    C.AmountReceverDay, C.AsyncID, C.AsyncState, C.SelectState, C.SaleName,
    C.IsLegal, C.IsFakeSale, C.ReceiptName, C.CustomerImage, C.CreatedDate, C.UpdatedDate;
GO

CREATE OR ALTER VIEW [dbo].[View_CustomerMonthPaymentDevice]
AS
WITH SalesData AS (
    SELECT
        CustomerID,
        ROUND(SUM(ISNULL(AmountTotalSalesDenar, 0)), -3) AS AmountTotalSales,
        ROUND(SUM(ISNULL(AmountTotalCostDenar, 0)), -3) AS CostTotalSales,
        ROUND(SUM(ISNULL(AmountDaySalesDenar, 0)), -3) AS AmountDaySales,
        MAX(DateCreate) AS DateSaleDevice
    FROM View_CustomersSalesDelegate
    GROUP BY CustomerID
),
PaymentData AS (
    SELECT
        CustomerID,
        ROUND(SUM(ISNULL(AmountDenar, 0)), -3) AS ReceiptsTotal,
        MAX(PaymentDate) AS LastPaymentDate,
        COUNT(CustomerPaymentID) AS CountReceiptDevice
    FROM View_CustomersPaymentsDelegate
    GROUP BY CustomerID
),
AddToBoxData AS (
    SELECT
        CustomerIDPayment AS CustomerID,
        SUM(ISNULL(Amount, 0)) AS TotalAmount
    FROM View_AddToBox
    GROUP BY CustomerIDPayment
),
ItemsData AS (
    SELECT CustomerID, ItemsNames
    FROM View_CustomersTempItemsNames
)
SELECT
    C.*,
    MAX(ISNULL(D.DelegateName, '')) AS DelegateName,
    MAX(ISNULL(U.UserName, '')) AS UserName,
    MAX(ISNULL(CI.CityName, '')) AS CityName,
    MAX(SD.DateSaleDevice) AS DateSaleDevice,
    MAX(SD.AmountTotalSales) AS AmountTotalSales,
    MAX(SD.CostTotalSales) AS CostTotalSales,
    MAX(SD.AmountDaySales) AS AmountDaySales,
    MAX(PD.ReceiptsTotal) AS ReceiptsTotal,
    ROUND(MAX(SD.AmountTotalSales) - MAX(PD.ReceiptsTotal), -3) AS AmountRemaining,
    FLOOR(
        CASE
            WHEN ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)) <> 0
            THEN ((MAX(ATB.TotalAmount) * 100.0) / ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)))
            ELSE 0
        END
    ) AS ReceiptRateDevice,
    ISNULL(
        DATEDIFF(DAY,
            MAX(SD.DateSaleDevice),
            CASE
                WHEN (MAX(SD.AmountTotalSales) - MAX(PD.ReceiptsTotal)) <= 0 AND MAX(PD.LastPaymentDate) IS NOT NULL
                THEN MAX(PD.LastPaymentDate)
                ELSE GETDATE()
            END
        ) + 1, 0
    ) AS NumberOfDayDevice,
    (SELECT TOP 1 PhoneNumber FROM CompanyInformation) AS PhoneNumberCompany,
    MAX(PD.CountReceiptDevice) + 1 AS CountReceiptDevice,
    MAX(ISNULL(ID.ItemsNames, '')) AS ItemsNames,
    MAX(PD.LastPaymentDate) AS LastPaymentDate,
    ISNULL(DATEDIFF(DAY, MAX(PD.LastPaymentDate), GETDATE()) + 1, 0) AS NumberOfDayPayment,
    MAX(COALESCE(WF.Amount1, 0)) AS Amount1,
    MAX(COALESCE(WF.Amount2, 0)) AS Amount2,
    MAX(COALESCE(WF.Amount3, 0)) AS Amount3,
    MAX(COALESCE(WF.Amount4, 0)) AS Amount4,
    MAX(COALESCE(WF.Amount5, 0)) AS Amount5,
    MAX(COALESCE(WF.Amount6, 0)) AS Amount6,
    MAX(COALESCE(WF.Amount7, 0)) AS Amount7,
    MAX(COALESCE(WF.Amount8, 0)) AS Amount8,
    MAX(COALESCE(WF.Amount9, 0)) AS Amount9,
    MAX(COALESCE(WF.Amount10, 0)) AS Amount10,
    MAX(COALESCE(WF.Amount11, 0)) AS Amount11,
    MAX(COALESCE(WF.Amount12, 0)) AS Amount12,
    MAX(COALESCE(WF.Amount13, 0)) AS Amount13,
    MAX(COALESCE(WF.Amount14, 0)) AS Amount14,
    MAX(COALESCE(WF.Amount15, 0)) AS Amount15,
    MAX(COALESCE(WF.Amount16, 0)) AS Amount16,
    MAX(COALESCE(WF.Amount17, 0)) AS Amount17,
    MAX(COALESCE(WF.Amount18, 0)) AS Amount18,
    MAX(COALESCE(WF.Amount19, 0)) AS Amount19,
    MAX(COALESCE(WF.Amount20, 0)) AS Amount20,
    MAX(COALESCE(WF.Amount21, 0)) AS Amount21,
    MAX(COALESCE(WF.Amount22, 0)) AS Amount22,
    MAX(COALESCE(WF.Amount23, 0)) AS Amount23,
    MAX(COALESCE(WF.Amount24, 0)) AS Amount24,
    MAX(COALESCE(WF.Amount25, 0)) AS Amount25,
    MAX(COALESCE(WF.Amount26, 0)) AS Amount26,
    MAX(COALESCE(WF.Amount27, 0)) AS Amount27,
    MAX(COALESCE(WF.Amount28, 0)) AS Amount28,
    MAX(COALESCE(WF.Amount29, 0)) AS Amount29,
    MAX(COALESCE(WF.Amount30, 0)) AS Amount30
FROM dbo.Customers C
LEFT JOIN Delegates D ON C.DelegateID = D.DelegateID
LEFT JOIN Users U ON C.UserID = U.UserID
LEFT JOIN Cities CI ON C.CityID = CI.CityID
LEFT JOIN SalesData SD ON C.CustomerID = SD.CustomerID
LEFT JOIN PaymentData PD ON C.CustomerID = PD.CustomerID
LEFT JOIN AddToBoxData ATB ON C.CustomerID = ATB.CustomerID
LEFT JOIN ItemsData ID ON C.CustomerID = ID.CustomerID
LEFT JOIN View_MonthFinal WF ON C.CustomerID = WF.CustomerID
GROUP BY
    C.CustomerID, C.DelegateID, C.UserID, C.CityID, C.CustomerName, C.Address,
    C.Longitude, C.Latitude, C.Notes, C.PhoneNumber, C.CustomerState, C.ShopName,
    C.StoreAddress, C.NearestFunctionPoint, C.StorePhoneNumber, C.Neighborhood,
    C.AmountReceverDay, C.AsyncID, C.AsyncState, C.SelectState, C.SaleName,
    C.IsLegal, C.IsFakeSale, C.ReceiptName, C.CustomerImage, C.CreatedDate, C.UpdatedDate;
GO

CREATE OR ALTER VIEW [dbo].[View_CustomersDelegate]
AS
WITH CityData AS (
    SELECT CityID, CityName FROM Cities
),
DelegateData AS (
    SELECT DelegateID, DelegateName FROM Delegates
),
SalesData AS (
    SELECT
        CustomerID,
        MAX(DateCreate) AS DateSaleDevice,
        ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), -3) AS AmountTotalSales,
        ROUND(ISNULL(SUM(AmountTotalCostDenar), 0), -3) AS CostTotalSales,
        ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), -3) AS AmountDaySales
    FROM View_CustomersSalesDelegate
    GROUP BY CustomerID
),
PaymentData AS (
    SELECT
        CustomerID,
        ROUND(ISNULL(SUM(AmountDenar), 0), -3) AS ReceiptsTotal,
        MAX(PaymentDate) AS LastPaymentDate,
        COUNT(CustomerPaymentID) AS CountReceiptDevice
    FROM View_CustomersPaymentsDelegate
    GROUP BY CustomerID
),
RecentPaymentData AS (
    SELECT CustomerID, ISNULL(SUM(AmountDenar), 0) AS RecentPaymentsTotal
    FROM View_CustomersPaymentsDelegate
    WHERE PaymentDate >= DATEADD(MONTH, -2, GETDATE())
    GROUP BY CustomerID
),
ItemsData AS (
    SELECT CustomerID, ItemsNames FROM View_CustomersTempItemsNames
)
SELECT
    C.CustomerID, C.DelegateID, C.UserID, C.CityID, C.CustomerName, C.Address,
    C.Longitude, C.Latitude, C.CustomerImage, C.Notes, C.PhoneNumber, C.CustomerState,
    C.ShopName, C.StoreAddress, C.NearestFunctionPoint, C.StorePhoneNumber, C.Neighborhood,
    C.AmountReceverDay, C.AsyncState, C.AsyncID, C.SelectState, C.SaleName, C.ReceiptName,
    C.IsLegal, C.IsFakeSale,
    CityData.CityName,
    DelegateData.DelegateName,
    SalesData.DateSaleDevice,
    ISNULL(SalesData.AmountTotalSales, 0) AS AmountTotalSales,
    ISNULL(SalesData.CostTotalSales, 0) AS CostTotalSales,
    ISNULL(SalesData.AmountDaySales, 0) AS AmountDaySales,
    ISNULL(PaymentData.ReceiptsTotal, 0) AS ReceiptsTotal,
    ROUND(ISNULL(SalesData.AmountTotalSales, 0) - ISNULL(PaymentData.ReceiptsTotal, 0), -3) AS AmountRemaining,
    ItemsData.ItemsNames,
    PaymentData.LastPaymentDate,
    ISNULL(PaymentData.CountReceiptDevice, 0) AS CountReceiptDevice,
    CASE
        WHEN ISNULL(SalesData.AmountTotalSales, 0) > 0
        THEN CAST(FLOOR((ISNULL(RecentPaymentData.RecentPaymentsTotal, 0) * 100.0) / SalesData.AmountTotalSales) AS INT)
        ELSE 0
    END AS ReceiptRateDevice,
    ISNULL(
        DATEDIFF(DAY,
            SalesData.DateSaleDevice,
            CASE
                WHEN ISNULL(SalesData.AmountTotalSales, 0) - ISNULL(PaymentData.ReceiptsTotal, 0) <= 0
                     AND PaymentData.LastPaymentDate IS NOT NULL
                    THEN PaymentData.LastPaymentDate
                ELSE GETDATE()
            END
        ) + 1, 0
    ) AS NumberOfDayDevice
FROM dbo.Customers AS C
LEFT JOIN CityData ON C.CityID = CityData.CityID
LEFT JOIN DelegateData ON C.DelegateID = DelegateData.DelegateID
LEFT JOIN SalesData ON C.CustomerID = SalesData.CustomerID
LEFT JOIN PaymentData ON C.CustomerID = PaymentData.CustomerID
LEFT JOIN RecentPaymentData ON C.CustomerID = RecentPaymentData.CustomerID
LEFT JOIN ItemsData ON C.CustomerID = ItemsData.CustomerID;
GO

IF OBJECT_ID(N'dbo.CustomerWeekDecisions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWeekDecisions (
        DecisionID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerID INT NOT NULL,
        UserID INT NOT NULL,
        DecisionType NVARCHAR(50) NOT NULL,
        WeekPaid FLOAT NULL,
        AmountTotalSales FLOAT NULL,
        PaidPercent FLOAT NULL,
        WeekStartDate DATE NULL,
        WeekEndDate DATE NULL,
        SnoozeUntil DATETIME NULL,
        Note NVARCHAR(MAX) NULL,
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE())
    );
    CREATE INDEX IX_CustomerWeekDecisions_CustomerID ON dbo.CustomerWeekDecisions (CustomerID);
END
GO

IF OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerNotes (
        NoteID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerID INT NOT NULL,
        UserID INT NOT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE())
    );
    CREATE INDEX IX_CustomerNotes_CustomerID ON dbo.CustomerNotes (CustomerID);
END
GO

IF OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.CustomerNotes
    WHERE NoteText LIKE N'تم تصنيف بيع الزبون%'
       OR NoteText LIKE N'تم إرسال الزبون%'
       OR NoteText LIKE N'مدير الفرع متواصل مع الزبون%';
END
GO

IF OBJECT_ID(N'dbo.DecisionNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DecisionNotifications (
        NotificationID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DecisionID INT NOT NULL,
        IsRead BIT NOT NULL DEFAULT (0),
        CreatedDate DATETIME NOT NULL DEFAULT (GETDATE())
    );
    CREATE INDEX IX_DecisionNotifications_DecisionID ON dbo.DecisionNotifications (DecisionID);
END
GO

CREATE OR ALTER PROC [dbo].[Customers_GetWeakWeekPayers]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        V.*,
        (ISNULL(V.Amount1, 0) + ISNULL(V.Amount2, 0) + ISNULL(V.Amount3, 0)
            + ISNULL(V.Amount4, 0) + ISNULL(V.Amount5, 0) + ISNULL(V.Amount6, 0)
            + ISNULL(V.Amount7, 0)) AS WeekPaid,
        CASE
            WHEN ISNULL(V.AmountTotalSales, 0) = 0 THEN 0
            ELSE ROUND(
                ((ISNULL(V.Amount1, 0) + ISNULL(V.Amount2, 0) + ISNULL(V.Amount3, 0)
                    + ISNULL(V.Amount4, 0) + ISNULL(V.Amount5, 0) + ISNULL(V.Amount6, 0)
                    + ISNULL(V.Amount7, 0)) * 100.0) / V.AmountTotalSales
            , 2)
        END AS PaidPercent
    FROM View_CustomerWeekPaymentDevice V
    WHERE ISNULL(V.AmountRemaining, 0) > 0
      AND ISNULL(V.AmountTotalSales, 0) > 0
      AND ISNULL(V.IsLegal, 0) = 0
      AND ISNULL(V.IsFakeSale, 0) = 0
      AND (
            (ISNULL(V.Amount1, 0) + ISNULL(V.Amount2, 0) + ISNULL(V.Amount3, 0)
                + ISNULL(V.Amount4, 0) + ISNULL(V.Amount5, 0) + ISNULL(V.Amount6, 0)
                + ISNULL(V.Amount7, 0)) * 100.0
          ) / V.AmountTotalSales <= 2
      AND NOT EXISTS (
            SELECT 1
            FROM dbo.Customers C
            WHERE C.CustomerID = V.CustomerID
              AND (ISNULL(C.IsLegal, 0) = 1 OR ISNULL(C.IsFakeSale, 0) = 1)
      )
      AND NOT EXISTS (
            SELECT 1
            FROM CustomerWeekDecisions D
            WHERE D.CustomerID = V.CustomerID
              AND (
                    D.DecisionType IN (N'قانونية', N'وهمي')
                    OR (
                        D.DecisionType = N'متواصل'
                        AND D.SnoozeUntil IS NOT NULL
                        AND D.SnoozeUntil > GETDATE()
                    )
              )
      )
    ORDER BY PaidPercent ASC, V.CustomerName;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_PostWeekDecision]
    @CustomerID INT,
    @UserID INT,
    @DecisionType NVARCHAR(50),
    @Note NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DecisionType NOT IN (N'متواصل', N'قانونية', N'وهمي')
    BEGIN
        RAISERROR(N'نوع القرار غير صالح', 16, 1);
        RETURN;
    END

    DECLARE @WeekPaid FLOAT = 0;
    DECLARE @AmountTotalSales FLOAT = 0;
    DECLARE @PaidPercent FLOAT = 0;
    DECLARE @CustomerName NVARCHAR(255) = N'';
    DECLARE @SnoozeUntil DATETIME = NULL;
    DECLARE @WeekStartDate DATE = CAST(GETDATE() - 7 AS DATE);
    DECLARE @WeekEndDate DATE = CAST(GETDATE() - 1 AS DATE);
    DECLARE @DecisionID INT;

    SELECT
        @WeekPaid = ISNULL(Amount1, 0) + ISNULL(Amount2, 0) + ISNULL(Amount3, 0)
            + ISNULL(Amount4, 0) + ISNULL(Amount5, 0) + ISNULL(Amount6, 0) + ISNULL(Amount7, 0),
        @AmountTotalSales = ISNULL(AmountTotalSales, 0),
        @CustomerName = ISNULL(CustomerName, N'')
    FROM View_CustomerWeekPaymentDevice
    WHERE CustomerID = @CustomerID;

    IF @CustomerName = N''
        SELECT @CustomerName = ISNULL(CustomerName, N'') FROM Customers WHERE CustomerID = @CustomerID;

    IF @AmountTotalSales > 0
        SET @PaidPercent = ROUND((@WeekPaid * 100.0) / @AmountTotalSales, 2);

    IF @DecisionType = N'متواصل'
        SET @SnoozeUntil = DATEADD(DAY, 7, GETDATE());

    INSERT INTO CustomerWeekDecisions
        (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
         WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
    VALUES
        (@CustomerID, @UserID, @DecisionType, @WeekPaid, @AmountTotalSales, @PaidPercent,
         @WeekStartDate, @WeekEndDate, @SnoozeUntil, @Note, GETDATE());

    SET @DecisionID = SCOPE_IDENTITY();

    IF @DecisionType = N'قانونية'
    BEGIN
        UPDATE Customers SET IsLegal = 1, UpdatedDate = GETDATE() WHERE CustomerID = @CustomerID;
        IF OBJECT_ID(N'dbo.CustomerWeekPaymentSnapshot', N'U') IS NOT NULL
            UPDATE CustomerWeekPaymentSnapshot SET IsLegal = 1 WHERE CustomerID = @CustomerID;
    END
    ELSE IF @DecisionType = N'وهمي'
    BEGIN
        UPDATE Customers SET IsFakeSale = 1, UpdatedDate = GETDATE() WHERE CustomerID = @CustomerID;
        IF OBJECT_ID(N'dbo.CustomerWeekPaymentSnapshot', N'U') IS NOT NULL
            UPDATE CustomerWeekPaymentSnapshot SET IsFakeSale = 1 WHERE CustomerID = @CustomerID;
    END

    INSERT INTO DecisionNotifications (DecisionID, IsRead, CreatedDate)
    VALUES (@DecisionID, 0, GETDATE());

    IF OBJECT_ID(N'dbo.Activities', N'U') IS NOT NULL
    BEGIN
        INSERT INTO Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
        VALUES (@UserID, N'قرار أسبوعي (' + @DecisionType + N') للزبون ' + @CustomerName, GETUTCDATE(), 'false', NEWID());
    END

    SELECT
        D.DecisionID, D.CustomerID, D.UserID, D.DecisionType, D.WeekPaid, D.AmountTotalSales,
        D.PaidPercent, D.WeekStartDate, D.WeekEndDate, D.SnoozeUntil, D.Note, D.CreatedDate,
        C.CustomerName, C.PhoneNumber, U.UserName, U.UserType,
        ISNULL(C.IsLegal, 0) AS IsLegal,
        ISNULL(C.IsFakeSale, 0) AS IsFakeSale
    FROM CustomerWeekDecisions D
    INNER JOIN Customers C ON C.CustomerID = D.CustomerID
    INNER JOIN Users U ON U.UserID = D.UserID
    WHERE D.DecisionID = @DecisionID;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_GetWeekDecisions]
    @DecisionType NVARCHAR(50) = NULL,
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @CustomerID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        D.DecisionID, D.CustomerID, D.UserID, D.DecisionType, D.WeekPaid, D.AmountTotalSales,
        D.PaidPercent, D.WeekStartDate, D.WeekEndDate, D.SnoozeUntil, D.Note, D.CreatedDate,
        C.CustomerName, C.PhoneNumber, U.UserName, U.UserType,
        ISNULL(C.IsLegal, 0) AS IsLegal,
        ISNULL(C.IsFakeSale, 0) AS IsFakeSale
    FROM CustomerWeekDecisions D
    INNER JOIN Customers C ON C.CustomerID = D.CustomerID
    INNER JOIN Users U ON U.UserID = D.UserID
    WHERE (@DecisionType IS NULL OR @DecisionType = N'' OR @DecisionType = N'الكل' OR D.DecisionType = @DecisionType)
      AND (@FromDate IS NULL OR D.CreatedDate >= @FromDate)
      AND (@ToDate IS NULL OR D.CreatedDate < DATEADD(DAY, 1, @ToDate))
      AND (@CustomerID IS NULL OR D.CustomerID = @CustomerID)
    ORDER BY D.CreatedDate DESC;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_GetDecisionNotifications]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        N.NotificationID, N.DecisionID, N.IsRead, N.CreatedDate,
        D.CustomerID, D.DecisionType, D.WeekPaid, D.PaidPercent, D.Note,
        C.CustomerName, U.UserName
    FROM DecisionNotifications N
    INNER JOIN CustomerWeekDecisions D ON D.DecisionID = N.DecisionID
    INNER JOIN Customers C ON C.CustomerID = D.CustomerID
    INNER JOIN Users U ON U.UserID = D.UserID
    ORDER BY N.CreatedDate DESC;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_ReadDecisionNotification]
    @NotificationID INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE DecisionNotifications SET IsRead = 1 WHERE NotificationID = @NotificationID;
    SELECT 1 AS Success;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_ReadAllDecisionNotifications]
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE DecisionNotifications SET IsRead = 1 WHERE IsRead = 0;
    SELECT 1 AS Success;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_GetCustomerNotes]
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT N.NoteID, N.CustomerID, N.UserID, N.NoteText, N.CreatedDate, U.UserName, U.UserType
    FROM CustomerNotes N
    INNER JOIN Users U ON U.UserID = N.UserID
    WHERE N.CustomerID = @CustomerID
    ORDER BY N.CreatedDate DESC;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_PostCustomerNote]
    @CustomerID INT,
    @UserID INT,
    @NoteText NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO CustomerNotes (CustomerID, UserID, NoteText, CreatedDate)
    VALUES (@CustomerID, @UserID, @NoteText, GETDATE());
    DECLARE @NoteID INT = SCOPE_IDENTITY();
    SELECT N.NoteID, N.CustomerID, N.UserID, N.NoteText, N.CreatedDate, U.UserName, U.UserType
    FROM CustomerNotes N
    INNER JOIN Users U ON U.UserID = N.UserID
    WHERE N.NoteID = @NoteID;
END
GO

CREATE OR ALTER PROC [dbo].[Customers_GetWeekReceipt]
    @DelegateID INT = NULL,
    @ShowType NVARCHAR(50) = NULL
AS
BEGIN
    IF @ShowType = N'الجميع'
    BEGIN
        SELECT * FROM View_CustomerWeekPaymentDevice
        WHERE (@DelegateID IS NULL OR DelegateID = @DelegateID)
          AND ISNULL(IsFakeSale, 0) = 0
    END

    IF @ShowType = N'المسددين'
    BEGIN
        SELECT * FROM View_CustomerWeekPaymentDevice
        WHERE (@DelegateID IS NULL OR DelegateID = @DelegateID)
          AND (Amount1 > 0 OR Amount2 > 0 OR Amount3 > 0 OR Amount4 > 0 OR Amount5 > 0 OR Amount6 > 0 OR Amount7 > 0)
          AND AmountRemaining > 0
          AND ISNULL(IsFakeSale, 0) = 0
    END

    IF @ShowType = N'المتوقفين'
    BEGIN
        SELECT * FROM View_CustomerWeekPaymentDevice
        WHERE (@DelegateID IS NULL OR DelegateID = @DelegateID)
          AND Amount1 = 0 AND Amount2 = 0 AND Amount3 = 0 AND Amount4 = 0
          AND Amount5 = 0 AND Amount6 = 0 AND Amount7 = 0
          AND AmountRemaining > 0
          AND ISNULL(IsFakeSale, 0) = 0
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Customers_Follow]
    @DelegateID INT,
    @PaymentDate DATETIME,
    @ShowType NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PaymentDateOnly DATE = CAST(@PaymentDate AS DATE);

    ;WITH CustomersBase AS
    (
        SELECT
            C.CustomerID, C.DelegateID, C.UserID, C.CityID, C.CustomerName, C.Address,
            C.Longitude, C.Latitude, C.Notes, C.PhoneNumber, C.CustomerState, C.ShopName,
            C.StoreAddress, C.NearestFunctionPoint, C.StorePhoneNumber, C.Neighborhood,
            C.AmountReceverDay, C.AsyncID, C.AsyncState, C.SelectState, C.SaleName,
            C.IsLegal, C.IsFakeSale, C.ReceiptName, C.CustomerImage, C.CreatedDate, C.UpdatedDate
        FROM dbo.Customers C
        WHERE (@DelegateID = 0 OR C.DelegateID = @DelegateID)
    ),
    SalesData AS
    (
        SELECT
            V.CustomerID,
            ROUND(COALESCE(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales,
            ROUND(COALESCE(SUM(V.AmountTotalCostDenar), 0), -3) AS CostTotalSales,
            ROUND(COALESCE(SUM(V.AmountDaySalesDenar), 0), -3) AS AmountDaySales,
            MAX(V.DateCreate) AS DateSaleDevice
        FROM View_CustomersSalesDelegate V
        GROUP BY V.CustomerID
    ),
    PaymentData AS
    (
        SELECT V.CustomerID, ROUND(COALESCE(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal
        FROM View_CustomersPaymentsDelegate V
        GROUP BY V.CustomerID
    ),
    AddToBoxData AS
    (
        SELECT V.CustomerIDPayment AS CustomerID, COALESCE(SUM(V.Amount), 0) AS TotalAmount
        FROM View_AddToBox V
        GROUP BY V.CustomerIDPayment
    ),
    ItemsData AS
    (
        SELECT V.CustomerID, STRING_AGG(CONCAT('(', V.ItemName, ' (', V.Quantity, ') )'), ' ') AS ItemsNames
        FROM View_SelectItemsSalesItemsNames V
        GROUP BY V.CustomerID
    ),
    TodayReceiptData AS
    (
        SELECT X.CustomerID, X.PaymentDate, ROUND(X.AmountDenar, -3) AS AmountDenar
        FROM (
            SELECT V.CustomerID, V.PaymentDate, V.AmountDenar,
                ROW_NUMBER() OVER (PARTITION BY V.CustomerID ORDER BY V.PaymentDate DESC) AS RN
            FROM View_ReceiptCustomerDate V
            WHERE V.PaymentDate >= @PaymentDateOnly
              AND V.PaymentDate < DATEADD(DAY, 1, @PaymentDateOnly)
        ) X
        WHERE X.RN = 1
    ),
    CustomerPaymentStats AS
    (
        SELECT CP.CustomerID, COUNT(*) AS CountReceiptDevice, MAX(CP.PaymentDate) AS LastPaymentDate
        FROM dbo.CustomersPayments CP
        GROUP BY CP.CustomerID
    ),
    CompanyData AS
    (
        SELECT TOP (1) PhoneNumber AS PhoneNumberCompany FROM dbo.CompanyInformation
    ),
    FinalData AS
    (
        SELECT
            C.CustomerID, C.DelegateID, C.UserID, C.CityID, C.CustomerName, C.Address,
            C.Longitude, C.Latitude, C.Notes, C.PhoneNumber, C.CustomerState, C.ShopName,
            C.StoreAddress, C.NearestFunctionPoint, C.StorePhoneNumber, C.Neighborhood,
            C.AmountReceverDay, C.AsyncID, C.AsyncState, C.SelectState, C.SaleName,
            C.IsLegal, C.IsFakeSale, C.ReceiptName, C.CustomerImage, C.CreatedDate, C.UpdatedDate,
            COALESCE(D.DelegateName, '') AS DelegateName,
            COALESCE(U.UserName, '') AS UserName,
            COALESCE(CI.CityName, '') AS CityName,
            SD.DateSaleDevice,
            COALESCE(SD.AmountTotalSales, 0) AS AmountTotalSales,
            COALESCE(SD.CostTotalSales, 0) AS CostTotalSales,
            COALESCE(SD.AmountDaySales, 0) AS AmountDaySales,
            COALESCE(PD.ReceiptsTotal, 0) AS ReceiptsTotal,
            ROUND(COALESCE(SD.AmountTotalSales, 0) - COALESCE(PD.ReceiptsTotal, 0), -3) AS AmountRemaining,
            FLOOR(
                CASE
                    WHEN SD.DateSaleDevice IS NULL THEN 0
                    WHEN ((DATEDIFF(DAY, SD.DateSaleDevice, GETDATE()) + 1) * COALESCE(NULLIF(SD.AmountDaySales, 0), 1)) = 0 THEN 0
                    ELSE (COALESCE(ATB.TotalAmount, 0) * 100.0 / ((DATEDIFF(DAY, SD.DateSaleDevice, GETDATE()) + 1) * COALESCE(NULLIF(SD.AmountDaySales, 0), 1)))
                END
            ) AS ReceiptRateDevice,
            CASE WHEN SD.DateSaleDevice IS NULL THEN 0 ELSE DATEDIFF(DAY, SD.DateSaleDevice, GETDATE()) + 1 END AS NumberOfDayDevice,
            CD.PhoneNumberCompany,
            COALESCE(CPS.CountReceiptDevice, 0) + 1 AS CountReceiptDevice,
            COALESCE(ID.ItemsNames, '') AS ItemsNames,
            COALESCE(CPS.LastPaymentDate, CONVERT(DATETIME, '1900-01-01')) AS LastPaymentDate,
            CASE WHEN CPS.LastPaymentDate IS NULL THEN 1 ELSE DATEDIFF(DAY, CPS.LastPaymentDate, GETDATE()) + 1 END AS NumberOfDayPayment,
            COALESCE(TRD.AmountDenar, 0) AS AmountReceipt
        FROM CustomersBase C
        LEFT JOIN dbo.Delegates D ON C.DelegateID = D.DelegateID
        LEFT JOIN dbo.Users U ON C.UserID = U.UserID
        LEFT JOIN dbo.Cities CI ON C.CityID = CI.CityID
        LEFT JOIN SalesData SD ON C.CustomerID = SD.CustomerID
        LEFT JOIN PaymentData PD ON C.CustomerID = PD.CustomerID
        LEFT JOIN AddToBoxData ATB ON C.CustomerID = ATB.CustomerID
        LEFT JOIN ItemsData ID ON C.CustomerID = ID.CustomerID
        LEFT JOIN CustomerPaymentStats CPS ON C.CustomerID = CPS.CustomerID
        LEFT JOIN TodayReceiptData TRD ON C.CustomerID = TRD.CustomerID
        CROSS JOIN CompanyData CD
    )
    SELECT *
    FROM FinalData
    WHERE ISNULL(IsFakeSale, 0) = 0
      AND (
            (@ShowType = N'المسددين' AND AmountReceipt > 0)
            OR (@ShowType <> N'المسددين' AND AmountReceipt = 0)
          )
    OPTION (RECOMPILE);
END
GO

PRINT N'WeakWeekPayers deploy completed on ' + DB_NAME();
GO
