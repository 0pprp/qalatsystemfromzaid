create   view [dbo].[View_SelectItemsWithdrawalTemporary]
AS
WITH ItemData AS (SELECT        ItemID, ItemName, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
                                          FROM            dbo.View_Items), UserData AS
    (SELECT        UserID, UserName
      FROM            dbo.Users)
    SELECT        SIWT.SelectItemWithdrawalID, SIWT.ItemID, SIWT.Quantity, SIWT.UserID, SIWT.AsyncState, SIWT.AsyncID, UD.UserName, ID.ItemName, ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, 
                              ISNULL(ID.ItemPriceDenar * SIWT.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(ID.ItemCostDenar * SIWT.Quantity, 0) AS ItemCostTotalDenar, ISNULL(ID.AmountDayDenar * SIWT.Quantity, 0) AS AmountDayTotalDenar
     FROM            dbo.SelectItemsWithdrawalTemporary AS SIWT LEFT OUTER JOIN
                              UserData AS UD ON SIWT.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SIWT.ItemID = ID.ItemID

