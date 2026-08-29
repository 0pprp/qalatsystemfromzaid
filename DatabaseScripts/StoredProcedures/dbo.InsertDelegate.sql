CREATE proc [dbo].[InsertDelegate]
@UserID int = NULL,
@CityID int = NULL,
@DelegateName nvarchar(255) = NULL,
@Address nvarchar(255) = NULL,
@PhoneNumber nvarchar(255) = NULL,
@Notes nvarchar(max) = NULL,
@ReceiptName nvarchar(255) = NULL,
@AsyncID nvarchar(255)  = NULL
as
DECLARE @BoxName nvarchar(255) = N'خزينة '+@DelegateName+''
exec InsertBox @BoxName =@BoxName,@UserID=@UserID
DECLARE @BoxID INT = (select top 1 BoxID from Boxes order by BoxID desc);
INSERT INTO [dbo].[Delegates]
           ([UserID]
           ,[CityID]
           ,[DelegateName]
           ,[Address]
           ,[PhoneNumber]
           ,[Notes]
           ,[DelegateState]
           ,[ProfitRatio]
           ,[AsyncState]
           ,[BoxID]
           ,[DeviceSaleState]
           ,[DevicePaymentState]
           ,[ReceiptName]
           ,[UpdateReceipt]
           ,[DeleteReceipt]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@CityID 
           ,@DelegateName 
           ,@Address 
           ,@PhoneNumber 
           ,@Notes 
           ,'true' 
           ,0.02
           ,'false'
           ,@BoxID 
           ,'true' 
           ,'true' 
           ,@ReceiptName 
           ,'true' 
           ,'true' 
		   ,@AsyncID)
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة الخزينة '+@BoxName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المندوب '+@DelegateName+N' رقم هاتفة '+@PhoneNumber+N' عنوانة '+@Address+N' واسم الجابي '+@ReceiptName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

