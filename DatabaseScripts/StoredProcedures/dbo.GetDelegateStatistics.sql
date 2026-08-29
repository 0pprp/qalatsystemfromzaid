 
CREATE proc [dbo].[GetDelegateStatistics]
as
WITH CustomerSalesSummary AS
(
    SELECT 
        DelegateID,
        COUNT(*) AS NumberOfCustomer
    FROM 
        CustomersSales
    GROUP BY 
        DelegateID
),
SalesDelegateSummary AS
(
    SELECT 
        DelegateID,
        ISNULL(SUM(AmountTotalSalesDenar), 0) AS AmountPrice,
        ISNULL(SUM(AmountTotalCostDenar), 0) AS AmountCost,
        ISNULL(SUM(AmountDaySalesDenar), 0) AS AmountDay,
        ISNULL(SUM(NumberOfItemsSales), 0) AS NumberOfItemSale
    FROM 
        View_CustomersSalesDelegate_Final
    GROUP BY 
        DelegateID
),
PaymentsDelegateSummary AS
(
    SELECT 
        DelegateID,
        ISNULL(SUM(AmountDenar), 0) AS AmountReceipt
    FROM 
        View_CustomersPaymentsDelegate_Final
    GROUP BY 
        DelegateID
),
CustomerZeroSummary AS
(
    SELECT 
        DelegateID,
        COUNT(*) AS NumberOfCustomerZero,
        ISNULL(SUM(AmountTotalSales), 0) AS AmountPriceZero,
        ISNULL(SUM(AmountDaySales), 0) AS AmountDayZero
    FROM 
        CustomerZeroRemainingByDate_Final
    WHERE 
        AmountRemaining = 0
    GROUP BY 
        DelegateID
)
SELECT 
    D.DelegateID,
    D.DelegateName,
    ISNULL(CS.NumberOfCustomer, 0) AS NumberOfCustomer,
    ISNULL(SDS.AmountPrice, 0) AS AmountPrice,
    ISNULL(SDS.AmountCost, 0) AS AmountCost,
    ISNULL(SDS.AmountDay, 0) AS AmountDay,
    ISNULL(SDS.NumberOfItemSale, 0) AS NumberOfItemSale,
    ISNULL(PDS.AmountReceipt, 0) AS AmountReceipt,
    ISNULL(CZS.NumberOfCustomerZero, 0) AS NumberOfCustomerZero,
    ISNULL(CZS.AmountPriceZero, 0) AS AmountPriceZero,
    ISNULL(CZS.AmountDayZero, 0) AS AmountDayZero
FROM 
    Delegates D
LEFT JOIN 
    CustomerSalesSummary CS ON D.DelegateID = CS.DelegateID
LEFT JOIN 
    SalesDelegateSummary SDS ON D.DelegateID = SDS.DelegateID
LEFT JOIN 
    PaymentsDelegateSummary PDS ON D.DelegateID = PDS.DelegateID
LEFT JOIN 
    CustomerZeroSummary CZS ON D.DelegateID = CZS.DelegateID
WHERE 
    D.DelegateState = 'true'
ORDER BY 
    D.DelegateID;


