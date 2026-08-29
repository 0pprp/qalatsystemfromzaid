create   view [dbo].[View_SelectItemsTransferStoresTemporary]
AS
WITH ItemData AS (SELECT        ItemID, ItemName, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
                                          FROM            dbo.View_Items), UserData AS
    (SELECT        UserID, UserName
      FROM            dbo.Users)
    SELECT        SITST.SelectItemTransferStoreTemporaryID, SITST.ItemID, SITST.Quantity, SITST.UserID, SITST.AsyncState, SITST.AsyncID, UD.UserName, ID.ItemName, ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, 
                              ISNULL(ID.ItemPriceDenar * SITST.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(ID.ItemCostDenar * SITST.Quantity, 0) AS ItemCostTotalDenar, ISNULL(ID.AmountDayDenar * SITST.Quantity, 0) AS AmountDayTotalDenar
     FROM            dbo.SelectItemsTransferStoresTemporary AS SITST LEFT OUTER JOIN
                              UserData AS UD ON SITST.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SITST.ItemID = ID.ItemID

