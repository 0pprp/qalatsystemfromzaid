create   view [dbo].[View_CustomerAddRequest]
AS
SELECT        dbo.CustomerAddRequest.CustomerAddRequestID, dbo.CustomerAddRequest.CityID, dbo.CustomerAddRequest.CustomerName, dbo.CustomerAddRequest.PhoneNumber, dbo.CustomerAddRequest.Address, 
                         dbo.CustomerAddRequest.ShopName, dbo.CustomerAddRequest.NearestFunctionPoint, dbo.CustomerAddRequest.Location, dbo.CustomerAddRequest.AsyncID, dbo.CustomerAddRequest.AsyncState, 
                         dbo.CustomerAddRequest.DateCreate, dbo.Cities.CityName AS CityName
FROM            dbo.CustomerAddRequest LEFT OUTER JOIN
                         dbo.Cities ON dbo.CustomerAddRequest.CityID = dbo.Cities.CityID

