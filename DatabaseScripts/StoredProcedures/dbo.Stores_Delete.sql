
CREATE proc [dbo].[Stores_Delete]
@StoreID int,
@UserDeleteID int
as
update Stores set 
State=0 
where StoreID=@StoreID
		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف المخزن '+(select StoreName from Stores where     StoreID=@StoreID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() ) 


