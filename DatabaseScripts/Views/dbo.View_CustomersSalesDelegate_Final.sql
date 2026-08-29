create   view [dbo].[View_CustomersSalesDelegate_Final]
AS
WITH SalesSummary AS (SELECT        CustomerSaleID, SUM(Quantity) AS TotalQuantity, SUM(ItemPriceDenar * Quantity) AS TotalAmountSales, SUM(AmountDayDenar * Quantity) AS TotalAmountDaySales, SUM(ItemCostDenar * Quantity) 
                                                                                 AS TotalAmountCost
                                                        FROM            dbo.View_SelectItemsSales
                                                        GROUP BY CustomerSaleID)
    SELECT        dbo.CustomersSales.CustomerSaleID, dbo.CustomersSales.DateCreate, dbo.CustomersSales.DelegateID, dbo.CustomersSales.CustomerID, ISNULL(SalesSummary_1.TotalQuantity, 0) AS NumberOfItemsSales, 
                              ISNULL(SalesSummary_1.TotalAmountSales, 0) - dbo.CustomersSales.DiscountAmountTotal * 1448 AS AmountTotalSalesDenar, ISNULL(SalesSummary_1.TotalAmountDaySales, 0) 
                              - dbo.CustomersSales.DiscountAmountTotalDay * 1448 AS AmountDaySalesDenar, ISNULL(SalesSummary_1.TotalAmountCost, 0) AS AmountTotalCostDenar
     FROM            dbo.CustomersSales LEFT OUTER JOIN
                              SalesSummary AS SalesSummary_1 ON dbo.CustomersSales.CustomerSaleID = SalesSummary_1.CustomerSaleID

