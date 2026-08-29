create   view [dbo].[View_CustomerOldRequest]
AS
SELECT        dbo.CustomerOldRequest.CustomerOldRequestID, dbo.CustomerOldRequest.CustomerID, dbo.CustomerOldRequest.DateCreate, dbo.CustomerOldRequest.AsyncID, dbo.CustomerOldRequest.AsyncState, 
                         ISNULL(dbo.Customers.CustomerName, '') AS CustomerName
FROM            dbo.CustomerOldRequest LEFT OUTER JOIN
                         dbo.Customers ON dbo.CustomerOldRequest.CustomerID = dbo.Customers.CustomerID

