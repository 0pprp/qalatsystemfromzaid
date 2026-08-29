 

CREATE VIEW [dbo].[View_Customers]
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
),

MainData AS (
    SELECT 
        C.CustomerID, 
        C.DelegateID, 
        C.UserID, 
        C.CityID, 
        C.CustomerName, 
        C.Address, 
        C.Longitude, 
        C.Latitude, 
        C.Notes, 
        C.PhoneNumber, 
        C.CustomerState, 
        C.ShopName, 
        C.StoreAddress, 
        C.NearestFunctionPoint, 
        C.StorePhoneNumber, 
        C.Neighborhood, 
        C.AmountReceverDay, 
        C.AsyncID, 
        C.AsyncState, 
        C.SelectState, 
        C.SaleName, 
        C.IsLegal,
        C.IsFakeSale, 
        C.ReceiptName, 
        C.CustomerImage,
        D.DelegateName,
        D.PhoneNumber AS PhoneNumberCompany,
        U.UserName,
        CI.CityName,
        SD.DateSaleDevice,
        ISNULL(SD.AmountTotalSales, 0) AS AmountTotalSales,
        ISNULL(SD.CostTotalSales, 0) AS CostTotalSales,
        ISNULL(SD.AmountDaySales, 0) AS AmountDaySales,
        ISNULL(PD.ReceiptsTotal, 0) AS ReceiptsTotal,
        ISNULL(SD.AmountTotalSales, 0) - ISNULL(PD.ReceiptsTotal, 0) AS AmountRemaining,
        ISNULL(ATB.TotalAmount, 0) AS TotalAmount,
        PD.LastPaymentDate,
        ISNULL(PD.CountReceiptDevice, 0) AS CountReceiptDevice,
        ID.ItemsNames,
        ISNULL(WF.Amount1, 0) AS Amount1,
        ISNULL(WF.Amount2, 0) AS Amount2,
        ISNULL(WF.Amount3, 0) AS Amount3,
        ISNULL(WF.Amount4, 0) AS Amount4,
        ISNULL(WF.Amount5, 0) AS Amount5,
        ISNULL(WF.Amount6, 0) AS Amount6,
        ISNULL(WF.Amount7, 0) AS Amount7
    FROM dbo.Customers C
    LEFT JOIN Delegates D ON C.DelegateID = D.DelegateID
    LEFT JOIN Users U ON C.UserID = U.UserID
    LEFT JOIN Cities CI ON C.CityID = CI.CityID
    LEFT JOIN SalesData SD ON C.CustomerID = SD.CustomerID
    LEFT JOIN PaymentData PD ON C.CustomerID = PD.CustomerID
    LEFT JOIN AddToBoxData ATB ON C.CustomerID = ATB.CustomerID
    LEFT JOIN ItemsData ID ON C.CustomerID = ID.CustomerID
    LEFT JOIN View_WeekFinal WF ON C.CustomerID = WF.CustomerID
)

SELECT 
    *,
    -- نسبة السداد
    FLOOR(CASE 
        WHEN ((DATEDIFF(DAY, DateSaleDevice, GETDATE()) + 1) * ISNULL(AmountDaySales, 0)) <> 0 
        THEN (ISNULL(TotalAmount, 0) * 100.0) / ((DATEDIFF(DAY, DateSaleDevice, GETDATE()) + 1) * ISNULL(AmountDaySales, 0))
        ELSE 0
    END) AS ReceiptRateDevice,

    -- عدد أيام الجهاز بناء على حالة الدفع
    ISNULL(DATEDIFF(DAY, 
        DateSaleDevice,
        CASE 
            WHEN ISNULL(AmountRemaining, 0) <= 0 AND LastPaymentDate IS NOT NULL THEN LastPaymentDate
            ELSE GETDATE()
        END
    ) + 1, 0) AS NumberOfDayDevice,

    -- عدد الأيام منذ آخر دفعة
    ISNULL(DATEDIFF(DAY, LastPaymentDate, GETDATE()) + 1, 0) AS NumberOfDayPayment

FROM MainData;

