create   view [dbo].[View_CustomersPaymentsMerchant]
AS
SELECT        CityID, CityName, UserID, CityNameState, AsyncState, AsyncID
FROM            dbo.Cities

