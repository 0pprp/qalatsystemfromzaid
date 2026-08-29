create   view [dbo].[View_CustomerPaymentBalance]
AS
SELECT        dbo.CustomerPaymentBalance.CustomerPaymentBalanceID, dbo.CustomerPaymentBalance.UserID, dbo.CustomerPaymentBalance.CustomerID, dbo.CustomerPaymentBalance.BoxID, 
                         dbo.CustomerPaymentBalance.DateCreate, dbo.CustomerPaymentBalance.BoundNumber, dbo.CustomerPaymentBalance.AccountZero, dbo.CustomerPaymentBalance.DelegateState, dbo.CustomerPaymentBalance.AsyncState, 
                         dbo.CustomerPaymentBalance.AsyncID, dbo.CustomerPaymentBalance.DelegateID, dbo.CustomerPaymentBalance.SelectState, ISNULL(dbo.Users.UserName, '') AS UserName, ISNULL(dbo.Customers.CustomerName, '') 
                         AS CustomerName, ISNULL(dbo.Customers.PhoneNumber, '') AS PhoneNumber, ISNULL(dbo.Boxes.BoxName, '') AS BoxName, ISNULL(dbo.Delegates.DelegateName, '') AS DelegateName, ISNULL(AddToBox.AmountDenar, 0) 
                         AS AmountDenar, ISNULL(dbo.Customers.CityID, 0) AS CityID, ISNULL(dbo.Cities.CityName, '') AS CityName
FROM            dbo.CustomerPaymentBalance LEFT OUTER JOIN
                         dbo.Users ON dbo.CustomerPaymentBalance.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.Customers ON dbo.CustomerPaymentBalance.CustomerID = dbo.Customers.CustomerID LEFT OUTER JOIN
                         dbo.Boxes ON dbo.CustomerPaymentBalance.BoxID = dbo.Boxes.BoxID LEFT OUTER JOIN
                         dbo.Delegates ON dbo.CustomerPaymentBalance.DelegateID = dbo.Delegates.DelegateID LEFT OUTER JOIN
                             (SELECT        CustomerPaymentBalanceID, SUM(Amount * 1448) AS AmountDenar
                               FROM            dbo.AddToBox AS AddToBox_1
                               GROUP BY CustomerPaymentBalanceID) AS AddToBox ON dbo.CustomerPaymentBalance.CustomerPaymentBalanceID = AddToBox.CustomerPaymentBalanceID LEFT OUTER JOIN
                         dbo.Cities ON dbo.Customers.CityID = dbo.Cities.CityID

