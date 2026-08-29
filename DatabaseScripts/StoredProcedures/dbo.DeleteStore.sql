CREATE proc [dbo].[DeleteStore]
@StoreID int = NULL,
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
           ,N'تم حذف المخزن '+(select StoreName from Stores where StoreID=@StoreID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Stores set State='false' where StoreID=@StoreID

