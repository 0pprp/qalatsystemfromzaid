/*
  Legal conversion pending: hide from BM WeakWeekPayers without setting IsLegal
  (delegate lists keep the customer until accountant approves).
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.LegalConversionRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegalConversionRequests (
        RequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LegalConversionRequests PRIMARY KEY,
        CustomerID INT NOT NULL,
        RequestedByUserID INT NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        RequestNote NVARCHAR(MAX) NULL,
        WeekDecisionId INT NULL,
        ApprovedDecisionId INT NULL,
        ReviewedByUserID INT NULL,
        ReviewNote NVARCHAR(MAX) NULL,
        RequestedAt DATETIME2 NOT NULL CONSTRAINT DF_LCR_RequestedAt DEFAULT (SYSUTCDATETIME()),
        ReviewedAt DATETIME2 NULL,
        WeekPaid FLOAT NULL,
        PaidPercent FLOAT NULL
    );
END
GO

SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_LegalConversion_PendingCustomer'
      AND object_id = OBJECT_ID(N'dbo.LegalConversionRequests')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_LegalConversion_PendingCustomer
        ON dbo.LegalConversionRequests (CustomerID)
        WHERE Status = N'Pending';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_LegalConversion_Status_RequestedAt'
      AND object_id = OBJECT_ID(N'dbo.LegalConversionRequests')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_LegalConversion_Status_RequestedAt
        ON dbo.LegalConversionRequests (Status, RequestedAt DESC)
        INCLUDE (CustomerID, RequestedByUserID);
END
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROC [dbo].[Customers_GetWeakWeekPayers]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDate DATETIME = CONVERT(DATETIME, CONVERT(DATE, DATEADD(DAY, -7, GETDATE())));
    DECLARE @ToDateExclusive DATETIME = CONVERT(DATETIME, CONVERT(DATE, GETDATE()));

    ;WITH WeekPaid AS (
        SELECT
            P.CustomerID,
            ROUND(SUM(ISNULL(A.Amount, 0) * 1448.0), -3) AS WeekPaid
        FROM dbo.CustomersPayments AS P WITH (NOLOCK)
        INNER JOIN dbo.AddToBox AS A WITH (NOLOCK)
            ON A.CustomerPaymentID = P.CustomerPaymentID
        WHERE P.PaymentDate >= @FromDate
          AND P.PaymentDate < @ToDateExclusive
        GROUP BY P.CustomerID
    ),
    SalesAgg AS (
        SELECT
            S.CustomerID,
            ROUND(SUM(ISNULL(S.AmountTotalSalesDenar, 0)), -3) AS AmountTotalSales,
            ROUND(SUM(ISNULL(S.AmountDaySalesDenar, 0)), -3) AS AmountDaySales,
            ROUND(SUM(ISNULL(S.AmountTotalCostDenar, 0)), -3) AS CostTotalSales,
            MAX(S.DateCreate) AS DateSaleDevice
        FROM dbo.View_CustomersSalesDelegate AS S
        GROUP BY S.CustomerID
        HAVING ROUND(SUM(ISNULL(S.AmountTotalSalesDenar, 0)), -3) > 0
    ),
    PaidAgg AS (
        SELECT
            P.CustomerID,
            ROUND(SUM(ISNULL(P.AmountDenar, 0)), -3) AS ReceiptsTotal,
            MAX(P.PaymentDate) AS LastPaymentDate,
            COUNT_BIG(P.CustomerPaymentID) AS CountReceiptDevice
        FROM dbo.View_CustomersPaymentsDelegate AS P
        GROUP BY P.CustomerID
    ),
    Blocked AS (
        SELECT DISTINCT D.CustomerID
        FROM dbo.CustomerWeekDecisions AS D WITH (NOLOCK)
        WHERE D.DecisionType IN (N'قانونية', N'وهمي', N'تنازل', N'طلب تنازل')
           OR (
                D.DecisionType = N'متواصل'
                AND D.SnoozeUntil IS NOT NULL
                AND D.SnoozeUntil > GETDATE()
           )
        UNION
        SELECT r.CustomerID
        FROM dbo.LegalConversionRequests AS r WITH (NOLOCK)
        WHERE r.Status = N'Pending'
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
        ISNULL(D.DelegateName, N'') AS DelegateName,
        ISNULL(U.UserName, N'') AS UserName,
        ISNULL(CI.CityName, N'') AS CityName,
        S.DateSaleDevice,
        S.AmountTotalSales,
        S.CostTotalSales,
        S.AmountDaySales,
        ISNULL(P.ReceiptsTotal, 0) AS ReceiptsTotal,
        ROUND(S.AmountTotalSales - ISNULL(P.ReceiptsTotal, 0), -3) AS AmountRemaining,
        CAST(0 AS FLOAT) AS ReceiptRateDevice,
        CAST(0 AS INT) AS NumberOfDayDevice,
        CAST(NULL AS NVARCHAR(50)) AS PhoneNumberCompany,
        CAST(ISNULL(P.CountReceiptDevice, 0) + 1 AS INT) AS CountReceiptDevice,
        CAST(N'' AS NVARCHAR(MAX)) AS ItemsNames,
        P.LastPaymentDate,
        CAST(0 AS INT) AS NumberOfDayPayment,
        CAST(0 AS FLOAT) AS Amount1,
        CAST(0 AS FLOAT) AS Amount2,
        CAST(0 AS FLOAT) AS Amount3,
        CAST(0 AS FLOAT) AS Amount4,
        CAST(0 AS FLOAT) AS Amount5,
        CAST(0 AS FLOAT) AS Amount6,
        CAST(0 AS FLOAT) AS Amount7,
        ISNULL(W.WeekPaid, 0) AS WeekPaid,
        CASE
            WHEN S.AmountTotalSales = 0 THEN CAST(0 AS FLOAT)
            ELSE ROUND((ISNULL(W.WeekPaid, 0) * 100.0) / S.AmountTotalSales, 2)
        END AS PaidPercent
    FROM dbo.Customers AS C WITH (NOLOCK)
    INNER JOIN SalesAgg AS S ON S.CustomerID = C.CustomerID
    LEFT JOIN PaidAgg AS P ON P.CustomerID = C.CustomerID
    LEFT JOIN WeekPaid AS W ON W.CustomerID = C.CustomerID
    LEFT JOIN dbo.Delegates AS D WITH (NOLOCK) ON D.DelegateID = C.DelegateID
    LEFT JOIN dbo.Users AS U WITH (NOLOCK) ON U.UserID = C.UserID
    LEFT JOIN dbo.Cities AS CI WITH (NOLOCK) ON CI.CityID = C.CityID
    WHERE ISNULL(C.IsLegal, 0) = 0
      AND ISNULL(C.IsFakeSale, 0) = 0
      AND ROUND(S.AmountTotalSales - ISNULL(P.ReceiptsTotal, 0), -3) > 0
      AND (
            CASE
                WHEN S.AmountTotalSales = 0 THEN 0
                ELSE (ISNULL(W.WeekPaid, 0) * 100.0) / S.AmountTotalSales
            END
          ) <= 3.8
      AND (
            P.LastPaymentDate IS NULL
            OR CONVERT(DATE, P.LastPaymentDate) <= DATEADD(DAY, -40, CONVERT(DATE, GETDATE()))
          )
      AND NOT EXISTS (SELECT 1 FROM Blocked B WHERE B.CustomerID = C.CustomerID)
    ORDER BY PaidPercent ASC, C.CustomerName;
END
GO

PRINT N'LegalConversion WeakWeekPayers patch OK on ' + DB_NAME();
