create   view [dbo].[View_ExchangeItems]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities), ExchangeItemDebts AS
    (SELECT        ExchangeItemID, SUM(CASE WHEN AccountType = N'علينا' THEN AmountDebt * 1448 ELSE 0 END) AS TotalDebtsOnUs, SUM(CASE WHEN AccountType = N'لنا' THEN AmountDebt * 1448 ELSE 0 END) 
                                AS TotalDebtsForUs
      FROM            dbo.ExchangeItemsDebts
      GROUP BY ExchangeItemID)
    SELECT        dbo.ExchangeItems.ExchangeItemID, dbo.ExchangeItems.UserID, dbo.ExchangeItems.CityID, dbo.ExchangeItems.ExchangeItemName, dbo.ExchangeItems.AsyncState, dbo.ExchangeItems.AsyncID, 
                              dbo.ExchangeItems.LimitAmount, dbo.ExchangeItems.ExchangeItemsState, UserData_1.UserName, CityData_1.CityName, ABS(ISNULL(ExchangeItemDebts_1.TotalDebtsOnUs, 0) 
                              - ISNULL(ExchangeItemDebts_1.TotalDebtsForUs, 0)) AS AmountAccount
     FROM            dbo.ExchangeItems LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.ExchangeItems.UserID = UserData_1.UserID LEFT OUTER JOIN
                              CityData AS CityData_1 ON dbo.ExchangeItems.CityID = CityData_1.CityID LEFT OUTER JOIN
                              ExchangeItemDebts AS ExchangeItemDebts_1 ON dbo.ExchangeItems.ExchangeItemID = ExchangeItemDebts_1.ExchangeItemID

