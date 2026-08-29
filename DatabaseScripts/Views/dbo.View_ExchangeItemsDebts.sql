create   view [dbo].[View_ExchangeItemsDebts]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), ExchangeItemData AS
    (SELECT        ExchangeItemID, ExchangeItemName, CityID
      FROM            dbo.ExchangeItems), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities)
    SELECT        dbo.ExchangeItemsDebts.ExchangeItemDebtID, dbo.ExchangeItemsDebts.UserID, dbo.ExchangeItemsDebts.ExchangeItemID, dbo.ExchangeItemsDebts.AmountDebt, dbo.ExchangeItemsDebts.DateDebt, 
                              dbo.ExchangeItemsDebts.Purpose, dbo.ExchangeItemsDebts.Notes, dbo.ExchangeItemsDebts.AccountType, dbo.ExchangeItemsDebts.AsyncID, dbo.ExchangeItemsDebts.AsyncState, UserData_1.UserName, 
                              ExchangeItemData_1.ExchangeItemName, ExchangeItemData_1.CityID, CityData_1.CityName, ISNULL(dbo.ExchangeItemsDebts.AmountDebt * 1448, 0) AS AmountDebtDenar
     FROM            dbo.ExchangeItemsDebts LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.ExchangeItemsDebts.UserID = UserData_1.UserID LEFT OUTER JOIN
                              ExchangeItemData AS ExchangeItemData_1 ON dbo.ExchangeItemsDebts.ExchangeItemID = ExchangeItemData_1.ExchangeItemID LEFT OUTER JOIN
                              CityData AS CityData_1 ON ExchangeItemData_1.CityID = CityData_1.CityID

