
CREATE proc [dbo].[Suppliers_Update]
@SupplierID int,
@SupplierName nvarchar(100),
@Address nvarchar(100),
@PhoneNumber nvarchar(100),
@Notes nvarchar(100),
@UserUpdateID int
as
update Suppliers set 
SupplierName = @SupplierName ,
Address = @Address,
PhoneNumber = @PhoneNumber,
Notes = @Notes
where SupplierID = @SupplierID
	    			INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل المورد  '+@SupplierName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
select * from View_Suppliers where SupplierID=@SupplierID 


