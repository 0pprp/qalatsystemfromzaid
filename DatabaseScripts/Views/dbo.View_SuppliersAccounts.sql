create   view [dbo].[View_SuppliersAccounts]
AS
SELECT        SA.SupplierAccountID, SA.SupplierID, SA.UserID, SA.BuyID, SA.Amount, SA.AccountType, SA.AccountsDate, SA.AsyncState, SA.AsyncID, SA.BuyBalanceID, U.UserName, S.SupplierName, S.SupplierState, S.CityID, C.CityName, 
                         ISNULL(SA.Amount * 1448, 0) AS AmountDenar
FROM            dbo.SuppliersAccounts AS SA LEFT OUTER JOIN
                         dbo.Users AS U ON SA.UserID = U.UserID LEFT OUTER JOIN
                         dbo.Suppliers AS S ON SA.SupplierID = S.SupplierID LEFT OUTER JOIN
                         dbo.Cities AS C ON S.CityID = C.CityID

