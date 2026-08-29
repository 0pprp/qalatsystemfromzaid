
CREATE proc [dbo].[InsertCustomerNoImage]
@DelegateID int = NULL,
@UserID int = NULL,
@CityID  int = NULL,
@CustomerName nvarchar(255) = NULL,
@Address nvarchar(255)= NULL,
@PhoneNumber nvarchar(255) = NULL,
@ShopName nvarchar(255) = NULL,
@NearestFunctionPoint nvarchar(255) = NULL,
@ReceiptName nvarchar(255) = NULL
as
 

INSERT INTO [dbo].[Customers]
           ([DelegateID]
           ,[UserID]
           ,[CityID]
           ,[CustomerName]
           ,[Address]
           ,[PhoneNumber]
           ,[CustomerState]
           ,[ShopName]
           ,[NearestFunctionPoint]
           ,[AsyncState]
           ,[ReceiptName]
		   ,[AsyncID])
     VALUES
           (@DelegateID  ,
@UserID  ,
@CityID   ,
@CustomerName  ,
@Address  ,
@PhoneNumber  ,
'true',
@ShopName  ,
@NearestFunctionPoint ,
'false',
@ReceiptName,
NEWID())

