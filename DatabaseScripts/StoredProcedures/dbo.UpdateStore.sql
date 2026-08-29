CREATE proc [dbo].[UpdateStore]
@StoreID int = NULL,
@UserID int = NULL,
@StoreName nvarchar(255),
@StorePlace nvarchar(255),
@Notes nvarchar(max),
@CityID int = NULL
as
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل المتجر من '+(select StoreName from Stores where StoreID=@StoreID)+N' الى '+@StoreName+N' ومن المكان '+(select StorePlace from Stores where StoreID=@StoreID)+N' الى '+@StorePlace+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

UPDATE [dbo].[Stores]
   SET [UserID] = @UserID 
      ,[StoreName] = @StoreName 
      ,[StorePlace] = @StorePlace 
      ,[Notes] = @Notes 
      ,[CityID] = @CityID 
 WHERE  StoreID=@StoreID

