create   view [dbo].[View_CustomersSalesZero]
AS
WITH ItemsSalesData AS (SELECT        CustomerSaleID, ISNULL(SUM(Quantity), 0) AS NumberOfItemsSales, ISNULL(SUM(ItemPriceDenar * Quantity), 0) AS AmountTotalDenar, ISNULL(SUM(AmountDayDenar * Quantity), 0) 
                                                                                  AS AmountTotalDayDenar
                                                         FROM            dbo.View_SelectItemsSalesQuantity
                                                         GROUP BY CustomerSaleID), AmountRemainingData AS
    (SELECT        CustomerID, ISNULL(SUM(AmountRemaining), 0) AS AmountRemaining
      FROM            dbo.CustomerAmountRemainingZero
      GROUP BY CustomerID), LastPaymentData AS
    (SELECT        CustomerID, MAX(PaymentDate) AS LastPaymentDate
      FROM            dbo.CustomersPayments
      GROUP BY CustomerID)
    SELECT        dbo.CustomersSales.CustomerSaleID, dbo.CustomersSales.CustomerID, dbo.CustomersSales.DelegateID, ISNULL(ItemsSalesData_1.NumberOfItemsSales, 0) AS NumberOfItemsSales, 
                              ISNULL(ItemsSalesData_1.AmountTotalDenar, 0) - dbo.CustomersSales.DiscountAmountTotal * 1448 AS AmountTotalSalesDenar, ISNULL(ItemsSalesData_1.AmountTotalDayDenar, 0) 
                              - dbo.CustomersSales.DiscountAmountTotalDay * 1448 AS AmountDaySalesDenar, ISNULL(AmountRemainingData_1.AmountRemaining, 0) AS AmountRemaining, LastPaymentData_1.LastPaymentDate
     FROM            dbo.CustomersSales LEFT OUTER JOIN
                              ItemsSalesData AS ItemsSalesData_1 ON dbo.CustomersSales.CustomerSaleID = ItemsSalesData_1.CustomerSaleID LEFT OUTER JOIN
                              AmountRemainingData AS AmountRemainingData_1 ON dbo.CustomersSales.CustomerID = AmountRemainingData_1.CustomerID LEFT OUTER JOIN
                              LastPaymentData AS LastPaymentData_1 ON dbo.CustomersSales.CustomerID = LastPaymentData_1.CustomerID

