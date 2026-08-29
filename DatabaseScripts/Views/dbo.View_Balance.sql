create   view [dbo].[View_Balance]
AS
SELECT        dbo.Balance.BalanceID, dbo.Balance.UserID, dbo.Balance.StoreBalanceID, dbo.Balance.BalanceTypeID, dbo.Balance.BalanceName, dbo.Balance.BalancePrice, dbo.Balance.BalanceCost, dbo.Balance.AmountDay, 
                         dbo.Balance.AsyncState, dbo.Balance.AsyncID, dbo.Balance.Notes, dbo.Balance.BalanceImage, dbo.Balance.BalanceState, dbo.Balance.StateDelete, dbo.Balance.SelectState, dbo.Users.UserName AS UserName, 
                         dbo.StoreBalance.StoreBalanceName AS StoreBalanceName, dbo.BalanceType.BalanceTypeName, dbo.Balance.BalancePrice * 1448 AS BalancePriceDenar, dbo.Balance.BalanceCost * 1448 AS BalanceCostDenar, 
                         dbo.Balance.AmountDay * 1448 AS AmountDayDenar
FROM            dbo.Balance LEFT OUTER JOIN
                         dbo.Users ON dbo.Balance.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.StoreBalance ON dbo.Balance.StoreBalanceID = dbo.StoreBalance.StoreBalanceID LEFT OUTER JOIN
                         dbo.BalanceType ON dbo.Balance.BalanceTypeID = dbo.BalanceType.BalanceTypeID

