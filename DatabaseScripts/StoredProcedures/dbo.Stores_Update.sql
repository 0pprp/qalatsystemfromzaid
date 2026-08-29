
CREATE proc [dbo].[Stores_Update]
@StoreID int,
@UserUpdateID int,
@StoreName nvarchar(100),
@StorePlace nvarchar(100),
@Notes  nvarchar(max)
as
update Stores set 
StoreName=@StoreName,
@StorePlace=@StorePlace,
@Notes=@Notes 
where StoreID=@StoreID
	    			INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل المخزن  '+@StoreName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
select * from View_Stores where StoreID=@StoreID 


