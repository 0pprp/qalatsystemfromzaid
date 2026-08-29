create   view [dbo].[View_DelegatesDebts]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), DelegateData AS
    (SELECT        DelegateID, DelegateName, CityID
      FROM            dbo.Delegates), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities)
    SELECT        dbo.DelegatesDebts.DelegateDebtID, dbo.DelegatesDebts.UserID, dbo.DelegatesDebts.DelegateID, dbo.DelegatesDebts.AmountDebt, dbo.DelegatesDebts.DateDebt, dbo.DelegatesDebts.Purpose, dbo.DelegatesDebts.Notes, 
                              dbo.DelegatesDebts.AccountType, dbo.DelegatesDebts.AsyncState, dbo.DelegatesDebts.AsyncID, UserData_1.UserName, DelegateData_1.DelegateName, DelegateData_1.CityID, CityData_1.CityName, 
                              ISNULL(dbo.DelegatesDebts.AmountDebt * 1448, 0) AS AmountDebtDenar
     FROM            dbo.DelegatesDebts LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.DelegatesDebts.UserID = UserData_1.UserID LEFT OUTER JOIN
                              DelegateData AS DelegateData_1 ON dbo.DelegatesDebts.DelegateID = DelegateData_1.DelegateID LEFT OUTER JOIN
                              CityData AS CityData_1 ON DelegateData_1.CityID = CityData_1.CityID

