CREATE proc [dbo].[UpdateSupplier]
@SupplierID int = NULL,
@UserID int = NULL,
@CityID int = NULL,
@SupplierName nvarchar(255),
@Address nvarchar(255),
@PhoneNumber nvarchar(255),
@Notes nvarchar(max) 
as
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل بيانات المورد من '+(select SupplierName from Suppliers where SupplierID=@SupplierID)+N' الى '+@SupplierName+N' و عنوانة من '+(select Address from Suppliers where SupplierID=@SupplierID)+N' الى '+@Address+N' و رقم هاتفة من '+(select PhoneNumber from Suppliers where SupplierID=@SupplierID)+N' الى '+@PhoneNumber+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
UPDATE [dbo].[Suppliers]
   SET [UserID] = @UserID 
      ,[CityID] = @CityID 
      ,[SupplierName] = @SupplierName 
      ,[Address] = @Address 
      ,[PhoneNumber] = @PhoneNumber 
      ,[Notes] = @Notes 
 WHERE  SupplierID=@SupplierID

