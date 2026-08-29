create   view [dbo].[View_SelectItemSaleBalance]
AS
WITH CustomerSaleBalanceData AS (SELECT        CustomerSaleBalanceID, CustomerID, AccountZero, ISNULL(DiscountAmountTotal, 0) AS DiscountAmountTotal, ISNULL(DiscountAmountTotalDay, 0) AS DiscountAmountTotalDay, 
                                                                                                          DateCreate
                                                                                 FROM            dbo.CustomerSaleBalance), UserData AS
    (SELECT        UserID, UserName
      FROM            dbo.Users), BalanceData AS
    (SELECT        BalanceID, BalanceTypeID, StoreBalanceID, BalanceName, ISNULL(BalancePrice * 1448, 0) AS BalancePriceDenar, ISNULL(BalanceCost * 1448, 0) AS BalanceCostDenar, ISNULL(AmountDay * 1448, 0) 
                                AS AmountDayDenar
      FROM            dbo.Balance), BalanceTypeData AS
    (SELECT        BalanceTypeID, BalanceTypeName
      FROM            dbo.BalanceType), StoreBalanceData AS
    (SELECT        StoreBalanceID, StoreBalanceName
      FROM            dbo.StoreBalance)
    SELECT        SSB.SelectItemSaleBalanceID, SSB.UserID, SSB.CustomerSaleBalanceID, SSB.BalanceID, SSB.AsyncState, SSB.AsyncID, CSB.CustomerID, CSB.AccountZero, CSB.DiscountAmountTotal, CSB.DiscountAmountTotalDay, 
                              CSB.DateCreate, UD.UserName, BD.BalanceTypeID, BD.StoreBalanceID, BD.BalanceName, BD.BalancePriceDenar, BD.BalanceCostDenar, BD.AmountDayDenar, BTD.BalanceTypeName, SBD.StoreBalanceName
     FROM            dbo.SelectItemSaleBalance AS SSB LEFT OUTER JOIN
                              CustomerSaleBalanceData AS CSB ON SSB.CustomerSaleBalanceID = CSB.CustomerSaleBalanceID LEFT OUTER JOIN
                              UserData AS UD ON SSB.UserID = UD.UserID LEFT OUTER JOIN
                              BalanceData AS BD ON SSB.BalanceID = BD.BalanceID LEFT OUTER JOIN
                              BalanceTypeData AS BTD ON BD.BalanceTypeID = BTD.BalanceTypeID LEFT OUTER JOIN
                              StoreBalanceData AS SBD ON BD.StoreBalanceID = SBD.StoreBalanceID

