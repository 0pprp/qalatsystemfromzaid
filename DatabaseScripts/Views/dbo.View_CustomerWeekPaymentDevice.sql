 
CREATE VIEW [dbo].[View_CustomerWeekPaymentDevice]
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
    SELECT 
        CustomerID,
        ItemsNames
    FROM 
        View_CustomersTempItemsNames
)

SELECT 
    C.*,
    MAX(ISNULL(D.DelegateName, '')) AS DelegateName,
    MAX(ISNULL(U.UserName, '')) AS UserName,
    MAX(ISNULL(CI.CityName, '')) AS CityName,

    -- مبيعات
    MAX(SD.DateSaleDevice) AS DateSaleDevice,
    MAX(SD.AmountTotalSales) AS AmountTotalSales,
    MAX(SD.CostTotalSales) AS CostTotalSales,
    MAX(SD.AmountDaySales) AS AmountDaySales,

    -- مدفوعات
    MAX(PD.ReceiptsTotal) AS ReceiptsTotal,
    ROUND(MAX(SD.AmountTotalSales) - MAX(PD.ReceiptsTotal), -3) AS AmountRemaining,

    -- نسبة الاستلام
    FLOOR(
        CASE 
            WHEN ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)) <> 0
            THEN ((MAX(ATB.TotalAmount) * 100.0) / ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)))
            ELSE 0
        END
    ) AS ReceiptRateDevice,

    -- عدد أيام الجهاز بناءً على حالة المبلغ المتبقي
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

    -- بيانات الأسبوع
    MAX(ISNULL(WF.Amount1, 0)) AS Amount1,
    MAX(ISNULL(WF.Amount2, 0)) AS Amount2,
    MAX(ISNULL(WF.Amount3, 0)) AS Amount3,
    MAX(ISNULL(WF.Amount4, 0)) AS Amount4,
    MAX(ISNULL(WF.Amount5, 0)) AS Amount5,
    MAX(ISNULL(WF.Amount6, 0)) AS Amount6,
    MAX(ISNULL(WF.Amount7, 0)) AS Amount7

FROM 
    dbo.Customers C
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
    C.IsLegal, C.IsFakeSale, C.ReceiptName, C.CustomerImage,C.CreatedDate,C.UpdatedDate;


