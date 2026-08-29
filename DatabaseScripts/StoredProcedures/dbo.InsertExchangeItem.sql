CREATE proc [dbo].[InsertExchangeItem]
@UserID int = NULL,
@CityID int = NULL,
@ExchangeItemName nvarchar(255) = NULL,
@LimitAmount float = NULL
as
 
INSERT INTO [dbo].[ExchangeItems]
           ([UserID]
           ,[CityID]
           ,[ExchangeItemName]
           ,[AsyncState]
           ,[LimitAmount]
           ,[ExchangeItemsState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@CityID 
           ,@ExchangeItemName 
           ,'false'
           ,@LimitAmount ,
           'true'
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة بند الصرف '+@ExchangeItemName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

