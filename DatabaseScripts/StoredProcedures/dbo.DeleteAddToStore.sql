CREATE proc [dbo].[DeleteAddToStore]
@AddToStoreID int = NULL,
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
           ,N'تم حذف '+(select ItemsNames from View_AddToStores where AddToStoreID=@AddToStoreID)+N' من الاضافات الى المخزن '+(select StoreName from View_AddToStores where AddToStoreID=@AddToStoreID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
exec DeleteAddToStoreAsyncID @AddToStoreID=@AddToStoreID
delete from AddToStores
where AddToStoreID=@AddToStoreID


