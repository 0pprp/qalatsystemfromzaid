create   view [dbo].[View_Suppliers]
AS
SELECT        S.SupplierID, S.UserID, S.CityID, S.SupplierName, S.Address, S.Longitude, S.Latitude, S.PhoneNumber, S.Notes, S.SupplierImage, S.SupplierState, S.AsyncState, S.AsyncID, U.UserName, C.CityName, ISNULL(SA.DebitAmount, 
                         0) - ISNULL(SA.CreditAmount, 0) AS AmountAccount
FROM            dbo.Suppliers AS S LEFT OUTER JOIN
                         dbo.Users AS U ON S.UserID = U.UserID LEFT OUTER JOIN
                         dbo.Cities AS C ON S.CityID = C.CityID LEFT OUTER JOIN
                             (SELECT        SupplierID, SUM(CASE WHEN AccountType = N'علينا' THEN Amount * 1448 ELSE 0 END) AS DebitAmount, SUM(CASE WHEN AccountType = N'لنا' THEN Amount * 1448 ELSE 0 END) AS CreditAmount
                               FROM            dbo.SuppliersAccounts
                               GROUP BY SupplierID) AS SA ON S.SupplierID = SA.SupplierID

