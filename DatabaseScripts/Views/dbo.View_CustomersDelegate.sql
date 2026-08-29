


CREATE view [dbo].[View_CustomersDelegate]
as
WITH CityData AS (
    SELECT 
        CityID, 
        CityName
    FROM 
        Cities
),
DelegateData AS (
    SELECT 
        DelegateID, 
        DelegateName
    FROM 
        Delegates
),
SalesData AS (
    SELECT 
        CustomerID,
        MAX(DateCreate) AS DateSaleDevice,
        ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), -3) AS AmountTotalSales,
        ROUND(ISNULL(SUM(AmountTotalCostDenar), 0), -3) AS CostTotalSales,
        ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), -3) AS AmountDaySales
    FROM 
        View_CustomersSalesDelegate
    GROUP BY 
        CustomerID
),
PaymentData AS (
    SELECT 
        CustomerID,
        ROUND(ISNULL(SUM(AmountDenar), 0), -3) AS ReceiptsTotal,
        MAX(PaymentDate) AS LastPaymentDate,
        COUNT(CustomerPaymentID) AS CountReceiptDevice
    FROM 
        View_CustomersPaymentsDelegate
    GROUP BY 
        CustomerID
),
RecentPaymentData AS (
    SELECT
        CustomerID,
        ISNULL(SUM(AmountDenar), 0) AS RecentPaymentsTotal
    FROM
        View_CustomersPaymentsDelegate
    WHERE
        PaymentDate >= DATEADD(MONTH, -2, GETDATE())
    GROUP BY
        CustomerID
),
ItemsData AS (
    SELECT 
        CustomerID,
        ItemsNames
    FROM 
        View_CustomersTempItemsNames
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

    -- نسبة التسديد لآخر شهرين من سعر البيع (عدد صحيح بدون كسور)
    CASE
        WHEN ISNULL(SalesData.AmountTotalSales, 0) > 0
        THEN CAST(FLOOR((ISNULL(RecentPaymentData.RecentPaymentsTotal, 0) * 100.0) / SalesData.AmountTotalSales) AS INT)
        ELSE 0
    END AS ReceiptRateDevice,

    -- ✅ الحساب الديناميكي لعدد الأيام حسب حالة التسديد
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

FROM 
    dbo.Customers AS C
LEFT JOIN 
    CityData ON C.CityID = CityData.CityID
LEFT JOIN 
    DelegateData ON C.DelegateID = DelegateData.DelegateID
LEFT JOIN 
    SalesData ON C.CustomerID = SalesData.CustomerID
LEFT JOIN 
    PaymentData ON C.CustomerID = PaymentData.CustomerID
LEFT JOIN
    RecentPaymentData ON C.CustomerID = RecentPaymentData.CustomerID
LEFT JOIN 
    ItemsData ON C.CustomerID = ItemsData.CustomerID;

