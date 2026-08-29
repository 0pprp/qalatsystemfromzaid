CREATE proc [dbo].[DeleteExchangeItems]
@ExchangeItemID int = NULL,
@UserID int = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف بند الصرف '+(select ExchangeItemName from ExchangeItems where ExchangeItemID=@ExchangeItemID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update ExchangeItems set ExchangeItemsState='false'
where ExchangeItemID=@ExchangeItemID

