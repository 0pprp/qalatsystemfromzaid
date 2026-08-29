CREATE proc [dbo].[InsertStore]
@UserID int = NULL,
@StoreName nvarchar(255)= NULL,
@StorePlace nvarchar(255)= NULL,
@Notes nvarchar(max)= NULL,
@CityID int = NULL
as
 

INSERT INTO [dbo].[Stores]
           ([UserID]
           ,[StoreName]
           ,[StorePlace]
           ,[Notes]
           ,[CityID]
           ,[AsyncState] 
           ,[State]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@StoreName 
           ,@StorePlace 
           ,@Notes 
           ,@CityID 
           ,'false'
           ,'true'
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المتجر '+@StoreName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

