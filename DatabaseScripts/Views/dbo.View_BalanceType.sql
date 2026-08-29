create   view [dbo].[View_BalanceType]
AS
SELECT        dbo.BalanceType.BalanceTypeID, dbo.BalanceType.BalanceTypeName, dbo.BalanceType.AsyncState, dbo.BalanceType.AsyncID, dbo.BalanceType.UserID, dbo.BalanceType.State, dbo.Users.UserName AS UserName, 
                         ISNULL(COUNT(dbo.Balance.BalanceID), 0) AS NumberOfBalance, ISNULL(COUNT(dbo.View_SelectItemSaleBalance.SelectItemSaleBalanceID), 0) AS NumberOfBalanceSale, 
                         ISNULL(SUM(CASE WHEN Balance.BalanceState = 'true' THEN 1 ELSE 0 END), 0) AS NumberOfCurrentBalance, ISNULL(COUNT(dbo.View_SelectBuyBalance.SelectBuyBalanceID), 0) AS NumberOfBalanceBuys, 
                         ISNULL(SUM(CASE WHEN Balance.BalanceState = 'true' THEN Balance.BalancePrice * 1448 ELSE 0 END), 0) AS PriceCurrentTotal, 
                         ISNULL(SUM(CASE WHEN Balance.BalanceState = 'true' THEN Balance.BalanceCost * 1448 ELSE 0 END), 0) AS CostCurrentTotal
FROM            dbo.BalanceType LEFT OUTER JOIN
                         dbo.Users ON dbo.BalanceType.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.Balance ON dbo.BalanceType.BalanceTypeID = dbo.Balance.BalanceTypeID LEFT OUTER JOIN
                         dbo.View_SelectItemSaleBalance ON dbo.BalanceType.BalanceTypeID = dbo.View_SelectItemSaleBalance.BalanceTypeID LEFT OUTER JOIN
                         dbo.View_SelectBuyBalance ON dbo.BalanceType.BalanceTypeID = dbo.View_SelectBuyBalance.BalanceTypeID
WHERE        (dbo.BalanceType.State = 'true')
GROUP BY dbo.BalanceType.BalanceTypeID, dbo.BalanceType.BalanceTypeName, dbo.BalanceType.AsyncState, dbo.BalanceType.AsyncID, dbo.BalanceType.UserID, dbo.BalanceType.State, dbo.Users.UserName

