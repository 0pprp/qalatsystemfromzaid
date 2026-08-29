CREATE proc [dbo].[InsertSupplier]
@UserID int = NULL,
@CityID int = NULL,
@SupplierName nvarchar(255)= NULL,
@Address nvarchar(255)= NULL,
@PhoneNumber nvarchar(255)= NULL,
@Notes nvarchar(max) = NULL
as
 

INSERT INTO [dbo].[Suppliers]
           ([UserID]
           ,[CityID]
           ,[SupplierName]
           ,[Address]
           ,[PhoneNumber]
           ,[Notes]
           ,[SupplierState]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@CityID 
           ,@SupplierName 
           ,@Address 
           ,@PhoneNumber 
           ,@Notes 
           ,'true'
           ,'false'
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المورد '+@SupplierName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

