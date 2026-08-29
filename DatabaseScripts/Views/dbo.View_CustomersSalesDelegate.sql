create   view [dbo].[View_CustomersSalesDelegate]
AS
WITH ItemsSalesData AS (SELECT        CustomerSaleID, ISNULL(SUM(Quantity), 0) AS NumberOfItemsSales, ISNULL(SUM(ItemPriceDenar * Quantity), 0) AS AmountTotalDenar, ISNULL(SUM(AmountDayDenar * Quantity), 0) 
                                                                                  AS AmountTotalDayDenar, ISNULL(SUM(ItemCostDenar * Quantity), 0) AS AmountTotalCostDenar
                                                         FROM            dbo.View_SelectItemsSales
                                                         GROUP BY CustomerSaleID), SalesCalculations AS
    (SELECT        View_SelectItemsSales_1.CustomerSaleID, ISNULL(SUM(View_SelectItemsSales_1.ItemPriceDenar * View_SelectItemsSales_1.Quantity), 0) - MAX(dbo.CustomersSales.DiscountAmountTotal * 1448) 
                                AS AmountTotalSalesDenar, ISNULL(SUM(View_SelectItemsSales_1.AmountDayDenar * View_SelectItemsSales_1.Quantity), 0) - MAX(dbo.CustomersSales.DiscountAmountTotalDay * 1448) AS AmountDaySalesDenar
      FROM            dbo.View_SelectItemsSales AS View_SelectItemsSales_1 INNER JOIN
                                dbo.CustomersSales ON View_SelectItemsSales_1.CustomerSaleID = dbo.CustomersSales.CustomerSaleID
      GROUP BY View_SelectItemsSales_1.CustomerSaleID)
    SELECT        CustomersSales_1.CustomerSaleID, CustomersSales_1.DateCreate, CustomersSales_1.DelegateID, CustomersSales_1.CustomerID, ItemsSalesData_1.NumberOfItemsSales, SalesCalculations_1.AmountTotalSalesDenar, 
                              SalesCalculations_1.AmountDaySalesDenar, ItemsSalesData_1.AmountTotalCostDenar
     FROM            dbo.CustomersSales AS CustomersSales_1 LEFT OUTER JOIN
                              ItemsSalesData AS ItemsSalesData_1 ON CustomersSales_1.CustomerSaleID = ItemsSalesData_1.CustomerSaleID LEFT OUTER JOIN
                              SalesCalculations AS SalesCalculations_1 ON CustomersSales_1.CustomerSaleID = SalesCalculations_1.CustomerSaleID

