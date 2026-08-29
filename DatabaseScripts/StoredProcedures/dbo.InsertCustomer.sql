 
CREATE proc [dbo].[InsertCustomer]
@DelegateID int = NULL,
@UserID int = NULL,
@CityID  int = NULL,
@CustomerName nvarchar(255) = NULL,
@Address nvarchar(255) = NULL,
@PhoneNumber nvarchar(255) = NULL,
@ShopName nvarchar(255) = NULL,
@NearestFunctionPoint nvarchar(255) = NULL,
@ReceiptName nvarchar(255)  = NULL ,
@SaleName nvarchar(255)  = NULL
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
		   ,[AsyncID]
		   ,[SaleName]
		   )
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
@ReceiptName ,
NEWID(),
@SaleName
)


INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة العميل '+@CustomerName+N' رقم هاتفة '+@PhoneNumber+N' عنوانة '+@Address+N' اسم محلة '+@ShopName+N' واسم البائع '+@SaleName+N' الى القائمة '+(select DelegateName from Delegates where DelegateID=@DelegateID)+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

  
 

 

