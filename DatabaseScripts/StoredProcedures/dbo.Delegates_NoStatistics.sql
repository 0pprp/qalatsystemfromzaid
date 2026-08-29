create proc [dbo].[Delegates_NoStatistics]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;
    WITH CustomerSalesSummary AS
    (
        SELECT 
            DelegateID,
            COUNT(*) AS NumberOfCustomer
        FROM 
            CustomersSales
        WHERE 
            CONVERT(DATE, DateCreate) BETWEEN @FromDate AND @ToDate
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
        WHERE 
            CONVERT(DATE, DateCreate) BETWEEN @FromDate AND @ToDate
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
        WHERE 
            CONVERT(DATE, PaymentDate) BETWEEN @FromDate AND @ToDate
        GROUP BY 
            DelegateID
    ) 
   
    SELECT 
        D.DelegateID,
        D.CityID,
        D.DelegateName,
        ISNULL(CS.NumberOfCustomer, 0) AS NumberOfCustomer,
        ISNULL(SDS.AmountPrice, 0) AS AmountPrice,
        ISNULL(SDS.AmountCost, 0) AS AmountCost,
        ISNULL(SDS.AmountDay, 0) AS AmountDay,
        ISNULL(SDS.NumberOfItemSale, 0) AS NumberOfItemSale,
        ISNULL(PDS.AmountReceipt, 0) AS AmountReceipt 
    FROM 
        Delegates D
    LEFT JOIN 
        CustomerSalesSummary CS ON D.DelegateID = CS.DelegateID
    LEFT JOIN 
        SalesDelegateSummary SDS ON D.DelegateID = SDS.DelegateID
    LEFT JOIN 
        PaymentsDelegateSummary PDS ON D.DelegateID = PDS.DelegateID
    WHERE 
        D.DelegateState = 'true'
    ORDER BY 
        D.DelegateID;

    SET NOCOUNT OFF;
END;

