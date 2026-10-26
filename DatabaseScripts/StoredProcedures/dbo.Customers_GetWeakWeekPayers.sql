CREATE OR ALTER PROC [dbo].[Customers_GetWeakWeekPayers]
AS
BEGIN
    SET NOCOUNT ON;

    -- نافذة متحركة: آخر 7 أيام حتى الأمس (بدون اليوم) — بدون CAST على العمود ليحافظ على الفهرس
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
    -- قانونية/وهمي يخرجون من ضعفاء المدير.
    -- طلب قانونية المعلّق (Pending) يخرج من ضعفاء المدير دون وسم IsLegal حتى موافقة المحاسب
    -- فيبقى الزبون بقائمة المندوب إلى أن يُؤشَّر قانونية.
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
      -- نفس فلتر تطبيق إدارة الفرع: آخر تسديد ≥ 40 يوماً (أو بلا تسديد أصلاً)
      AND (
            P.LastPaymentDate IS NULL
            OR CONVERT(DATE, P.LastPaymentDate) <= DATEADD(DAY, -40, CONVERT(DATE, GETDATE()))
          )
      AND NOT EXISTS (SELECT 1 FROM Blocked B WHERE B.CustomerID = C.CustomerID)
    ORDER BY PaidPercent ASC, C.CustomerName;
END
GO

-- فهرس يساعد نافذة الأسبوع (إن لم يكن موجوداً)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomersPayments_PaymentDate_Customer'
      AND object_id = OBJECT_ID(N'dbo.CustomersPayments')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CustomersPayments_PaymentDate_Customer
        ON dbo.CustomersPayments (PaymentDate)
        INCLUDE (CustomerID, CustomerPaymentID);
END
GO
