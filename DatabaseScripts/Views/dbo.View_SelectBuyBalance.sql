create   view [dbo].[View_SelectBuyBalance]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), BalanceData AS
    (SELECT        BalanceID, BalanceTypeID, StoreBalanceID, BalanceName, ISNULL(BalancePrice * 1448, 0) AS BalancePriceDenar, ISNULL(BalanceCost * 1448, 0) AS BalanceCostDenar, ISNULL(AmountDay * 1448, 0) 
                                AS AmountDayDenar
      FROM            dbo.Balance), BalanceTypeData AS
    (SELECT        BalanceTypeID, BalanceTypeName
      FROM            dbo.BalanceType), StoreBalanceData AS
    (SELECT        StoreBalanceID, StoreBalanceName
      FROM            dbo.StoreBalance), BuyBalanceData AS
    (SELECT        BuyBalanceID, DateCreate
      FROM            dbo.BuyBalance)
    SELECT        dbo.SelectBuyBalance.SelectBuyBalanceID, dbo.SelectBuyBalance.UserID, dbo.SelectBuyBalance.BalanceID, dbo.SelectBuyBalance.BuyBalanceID, dbo.SelectBuyBalance.AsyncID, dbo.SelectBuyBalance.AsyncState, 
                              UserData_1.UserName, BalanceData_1.BalanceTypeID, BalanceData_1.StoreBalanceID, BalanceTypeData_1.BalanceTypeName, StoreBalanceData_1.StoreBalanceName, BalanceData_1.BalanceName, 
                              BalanceData_1.BalancePriceDenar, BalanceData_1.BalanceCostDenar, BalanceData_1.AmountDayDenar, BuyBalanceData_1.DateCreate
     FROM            dbo.SelectBuyBalance LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.SelectBuyBalance.UserID = UserData_1.UserID LEFT OUTER JOIN
                              BalanceData AS BalanceData_1 ON dbo.SelectBuyBalance.BalanceID = BalanceData_1.BalanceID LEFT OUTER JOIN
                              BalanceTypeData AS BalanceTypeData_1 ON BalanceData_1.BalanceTypeID = BalanceTypeData_1.BalanceTypeID LEFT OUTER JOIN
                              StoreBalanceData AS StoreBalanceData_1 ON BalanceData_1.StoreBalanceID = StoreBalanceData_1.StoreBalanceID LEFT OUTER JOIN
                              BuyBalanceData AS BuyBalanceData_1 ON dbo.SelectBuyBalance.BuyBalanceID = BuyBalanceData_1.BuyBalanceID

