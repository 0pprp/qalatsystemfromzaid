 
CREATE VIEW [dbo].[View_Delegates]
AS
WITH CustomerCounts AS
(
    SELECT
        DelegateID,
        COUNT(CASE WHEN CustomerState = 'true' THEN CustomerID END) AS NumberOfCustomer,
        COUNT(CASE WHEN IsLegal = 'true' THEN CustomerID END) AS NumberOfCustomerIsLegal
    FROM dbo.Customers
    GROUP BY DelegateID
),
SalesData AS
(
    SELECT
        DelegateID,
        ISNULL(SUM(AmountTotalSales), 0) AS AmountTotal,
        ISNULL(SUM(CostTotalSales), 0) AS AmountCost,
        ISNULL(SUM(AmountDaySales), 0) AS AmountDay,
        ISNULL(SUM(ReceiptsTotal), 0) AS AmountRecever,
        ISNULL(SUM(AmountRemaining), 0) AS AmountRemaining
    FROM dbo.View_CustomersDelegate
    GROUP BY DelegateID
),
CustomerBalanceCounts AS
(
    SELECT
        DelegateID,
        COUNT(CASE WHEN AmountRemaining = 0 THEN CustomerID END) AS NumberOfCustomerIsZero,
        COUNT(CASE WHEN AmountRemaining > 0 THEN CustomerID END) AS NumberOfCustomerIsNotZero
    FROM dbo.View_CustomersDelegate
    GROUP BY DelegateID
),
BoxData AS
(
    SELECT BoxID, BoxName FROM dbo.Boxes
),
CityData AS
(
    SELECT CityID, CityName FROM dbo.Cities
)

SELECT
    D.DelegateID,
    D.UserID,
    D.CityID,
    D.DelegateName,
    D.Address,
    D.PhoneNumber,
    D.Notes,
    D.DelegateImage,
    D.DelegateState,
    D.ProfitRatio,
    D.SelectState,
    D.AsyncState,
    D.AsyncID,
    D.BoxID,
    D.BoxBalanceID,
    D.BalanceSaleState,
    D.DeviceSaleState,
    D.BalancePaymentState,
    D.DevicePaymentState,
    D.ReceiptName,
    D.UpdateReceipt,
    D.DeleteReceipt,

    ISNULL(CC.NumberOfCustomer, 0) AS NumberOfCustomer,
    ISNULL(CC.NumberOfCustomerIsLegal, 0) AS NumberOfCustomerIsLegal,

    ISNULL(SD.AmountTotal, 0) AS AmountTotal,
    ISNULL(SD.AmountCost, 0) AS AmountCost,
    ISNULL(SD.AmountDay, 0) AS AmountDay,
    ISNULL(SD.AmountRecever, 0) AS AmountRecever,
    ISNULL(SD.AmountRemaining, 0) AS AmountRemaining,

    CAST(0 AS FLOAT) AS AmountAccount,
    CAST(0 AS FLOAT) AS AmountTotalBalance,
    CAST(0 AS FLOAT) AS AmountCostBalance,
    CAST(0 AS FLOAT) AS AmountDayBalance,
    CAST(0 AS FLOAT) AS AmountReceverBalance,
    CAST(0 AS FLOAT) AS AmountRemainingBalance,

    BD.BoxName,
    ' ' AS BoxNameBalance,
    CD.CityName,

    CAST(0 AS FLOAT) AS ReceiptRateDevice,
    CAST(0 AS FLOAT) AS ReceiptRateBalance,
    CAST(0 AS FLOAT) AS ReceiptRateDayDevice,
    CAST(0 AS FLOAT) AS ReceiptRateDayBalance,

    ISNULL(CBC.NumberOfCustomerIsZero, 0) AS NumberOfCustomerIsZero,
    ISNULL(CBC.NumberOfCustomerIsNotZero, 0) AS NumberOfCustomerIsNotZero

FROM dbo.Delegates D
LEFT JOIN CustomerCounts CC
    ON D.DelegateID = CC.DelegateID
LEFT JOIN SalesData SD
    ON D.DelegateID = SD.DelegateID
LEFT JOIN CustomerBalanceCounts CBC
    ON D.DelegateID = CBC.DelegateID
LEFT JOIN BoxData BD
    ON D.BoxID = BD.BoxID
LEFT JOIN CityData CD
    ON D.CityID = CD.CityID

