create   view [dbo].[ViewAmountTotalSales_Final]
AS
SELECT        CS.CustomerID, COALESCE (SUM(VS.ItemPriceDenar * VS.Quantity), 0) - CS.DiscountAmountTotal * 1448 AS AmountTotalSalesDenar
FROM            dbo.CustomersSales AS CS LEFT OUTER JOIN
                         dbo.View_SelectItemsSales AS VS ON CS.CustomerSaleID = VS.CustomerSaleID
GROUP BY CS.CustomerID, CS.DiscountAmountTotal

