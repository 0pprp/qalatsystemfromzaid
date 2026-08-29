CREATE proc [dbo].[UpdateExchangeItem]
@ExchangeItemID int = NULL,
@UserID int = NULL,
@CityID int = NULL,
@ExchangeItemName nvarchar(255),
@LimitAmount float
as
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل بيانات بند الصرف من '+(select ExchangeItemName from ExchangeItems where ExchangeItemID=@ExchangeItemID)+N' الى '+@ExchangeItemName+N' و المبلغ المحدد من '+(select CONVERT(nvarchar(255),LimitAmount) from ExchangeItems where ExchangeItemID=@ExchangeItemID)+N' الى '+CONVERT(nvarchar(255),@LimitAmount)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
UPDATE [dbo].[ExchangeItems]
   SET [UserID] = @UserID 
      ,[CityID] = @CityID 
      ,[ExchangeItemName] = @ExchangeItemName 
      ,[LimitAmount] = @LimitAmount 
 WHERE  ExchangeItemID=@ExchangeItemID
 

