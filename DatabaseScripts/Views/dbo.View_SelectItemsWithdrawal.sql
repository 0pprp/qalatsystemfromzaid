create   view [dbo].[View_SelectItemsWithdrawal]
AS
WITH ItemData AS (SELECT        ItemID, ItemName, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
                                          FROM            dbo.View_Items), UserData AS
    (SELECT        UserID, UserName
      FROM            dbo.Users), WithdrawalStoresData AS
    (SELECT        WithdrawalStoresID, WithdrawalStoresDate AS DateCreate, StoreID, State
      FROM            dbo.WithdrawalStores), StoreData AS
    (SELECT        StoreID, StoreName
      FROM            dbo.Stores)
    SELECT        SIW.SelectItemWithdrawalID, SIW.WithdrawalStoresID, SIW.UserID, SIW.ItemID, SIW.Quantity, SIW.AsyncState, SIW.AsyncID, UD.UserName, ID.ItemName, ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, 
                              ISNULL(ID.ItemPriceDenar * SIW.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(ID.ItemCostDenar * SIW.Quantity, 0) AS ItemCostTotalDenar, ISNULL(ID.AmountDayDenar * SIW.Quantity, 0) AS AmountDayTotalDenar, 
                              WSD.DateCreate, WSD.StoreID, WSD.State, SD.StoreName
     FROM            dbo.SelectItemsWithdrawal AS SIW LEFT OUTER JOIN
                              UserData AS UD ON SIW.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SIW.ItemID = ID.ItemID LEFT OUTER JOIN
                              WithdrawalStoresData AS WSD ON SIW.WithdrawalStoresID = WSD.WithdrawalStoresID LEFT OUTER JOIN
                              StoreData AS SD ON WSD.StoreID = SD.StoreID

