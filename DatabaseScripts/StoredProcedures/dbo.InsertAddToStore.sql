CREATE proc [dbo].[InsertAddToStore]
@UserID int = NULL,
@DateAddToStore datetime = NULL,
@StoreID int  = NULL
as
INSERT INTO [dbo].[AddToStores]
           ([UserID]
           ,[DateAddToStore]
           ,[StoreID]
           ,[State]
           ,[AsyncState]
		    ,[AsyncID])
     VALUES
           (@UserID 
           ,@DateAddToStore 
           ,@StoreID 
           ,'true'
           ,'false',
		   NEWID())
 		
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة '+(select top 1 ItemsNames from View_AddToStores order by AddToStoreID desc)+N' هوت الى المخزن '+(select StoreName from Stores where StoreID=@StoreID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

