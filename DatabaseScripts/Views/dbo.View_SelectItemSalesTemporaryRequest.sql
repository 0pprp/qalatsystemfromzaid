create   view [dbo].[View_SelectItemSalesTemporaryRequest]
AS
WITH ItemData AS (SELECT        ItemID, ItemName
                                          FROM            dbo.Items), ViewItemData AS
    (SELECT        ItemID, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
      FROM            dbo.View_Items)
    SELECT        SIST.SelectItemSalesTemporaryRequestID, SIST.ItemID, SIST.DelegateID, SIST.Quantity, SIST.AsyncState, SIST.AsyncID, ID.ItemName, VID.ItemPriceDenar, VID.ItemCostDenar, VID.AmountDayDenar, 
                              ISNULL(VID.ItemPriceDenar * SIST.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(VID.ItemCostDenar * SIST.Quantity, 0) AS ItemCostTotalDenar, ISNULL(VID.AmountDayDenar * SIST.Quantity, 0) AS AmountDayTotalDenar
     FROM            dbo.SelectItemSalesTemporaryRequest AS SIST LEFT OUTER JOIN
                              ItemData AS ID ON SIST.ItemID = ID.ItemID LEFT OUTER JOIN
                              ViewItemData AS VID ON SIST.ItemID = VID.ItemID

