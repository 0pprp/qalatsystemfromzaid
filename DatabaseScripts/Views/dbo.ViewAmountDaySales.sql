create   view [dbo].[ViewAmountDaySales]
AS
SELECT        CS.CustomerID, ISNULL(SUM(VS.AmountDayDenar * VS.Quantity), 0) - CS.DiscountAmountTotalDay * 1448 AS AmountDaySalesDenar
FROM            dbo.CustomersSales AS CS LEFT OUTER JOIN
                         dbo.View_SelectItemsSales AS VS ON CS.CustomerSaleID = VS.CustomerSaleID
GROUP BY CS.CustomerID, CS.DiscountAmountTotalDay

