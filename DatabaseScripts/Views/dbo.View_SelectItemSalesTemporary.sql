create   view [dbo].[View_SelectItemSalesTemporary]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), ItemData AS
    (SELECT        ItemID, ItemName
      FROM            dbo.Items), ViewItemData AS
    (SELECT        ItemID, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
      FROM            dbo.View_Items)
    SELECT        SITS.SelectItemSalesTemporaryID, SITS.ItemID, SITS.UserID, SITS.Quantity, SITS.AsyncState, SITS.AsyncID, UD.UserName, ID.ItemName, VID.ItemPriceDenar, VID.ItemCostDenar, VID.AmountDayDenar, 
                              ISNULL(VID.ItemPriceDenar * SITS.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(VID.ItemCostDenar * SITS.Quantity, 0) AS ItemCostTotalDenar, ISNULL(VID.AmountDayDenar * SITS.Quantity, 0) AS AmountDayTotalDenar
     FROM            dbo.SelectItemSalesTemporary AS SITS LEFT OUTER JOIN
                              UserData AS UD ON SITS.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SITS.ItemID = ID.ItemID LEFT OUTER JOIN
                              ViewItemData AS VID ON SITS.ItemID = VID.ItemID

