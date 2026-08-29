create   view [dbo].[View_SelectItemsSalesRequest]
AS
WITH ItemData AS (SELECT        ItemID, ItemName, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
                                          FROM            dbo.View_Items)
    SELECT        SISR.SelectItemsSalesRequestID, SISR.CustomerSaleRequestID, SISR.ItemID, SISR.Quantity, ID.ItemName, ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, ISNULL(ID.ItemPriceDenar * SISR.Quantity, 0) 
                              AS ItemPriceTotalDenar, ISNULL(ID.ItemCostDenar * SISR.Quantity, 0) AS ItemCostTotalDenar, ISNULL(ID.AmountDayDenar * SISR.Quantity, 0) AS AmountDayTotalDenar
     FROM            dbo.SelectItemsSalesRequest AS SISR LEFT OUTER JOIN
                              ItemData AS ID ON SISR.ItemID = ID.ItemID

