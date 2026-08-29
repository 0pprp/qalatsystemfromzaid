 
CREATE VIEW [dbo].[View_CustomerMonthPaymentDevice]
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
    FROM View_CustomersTempItemsNames
)

SELECT 
    C.*,
    MAX(ISNULL(D.DelegateName, '')) AS DelegateName,
    MAX(ISNULL(U.UserName, '')) AS UserName,
    MAX(ISNULL(CI.CityName, '')) AS CityName,

    -- بيانات المبيعات
    MAX(SD.DateSaleDevice) AS DateSaleDevice,
    MAX(SD.AmountTotalSales) AS AmountTotalSales,
    MAX(SD.CostTotalSales) AS CostTotalSales,
    MAX(SD.AmountDaySales) AS AmountDaySales,

    -- بيانات المدفوعات
    MAX(PD.ReceiptsTotal) AS ReceiptsTotal,
    ROUND(MAX(SD.AmountTotalSales) - MAX(PD.ReceiptsTotal), -3) AS AmountRemaining,

    -- نسبة التسديد
    FLOOR(
        CASE 
            WHEN ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)) <> 0
            THEN ((MAX(ATB.TotalAmount) * 100.0) / ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)))
            ELSE 0
        END
    ) AS ReceiptRateDevice,

    -- عدد أيام الجهاز بناءً على المبلغ المتبقي
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

    -- باقي البيانات
    (SELECT TOP 1 PhoneNumber FROM CompanyInformation) AS PhoneNumberCompany,
    MAX(PD.CountReceiptDevice) + 1 AS CountReceiptDevice,
    MAX(ISNULL(ID.ItemsNames, '')) AS ItemsNames,
    MAX(PD.LastPaymentDate) AS LastPaymentDate,
    ISNULL(DATEDIFF(DAY, MAX(PD.LastPaymentDate), GETDATE()) + 1, 0) AS NumberOfDayPayment,

    -- بيانات الشهر
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

FROM 
    dbo.Customers C
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
    C.IsLegal, C.IsFakeSale, C.ReceiptName, C.CustomerImage,C.CreatedDate,C.UpdatedDate;

