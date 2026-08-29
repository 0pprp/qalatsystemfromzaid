
CREATE proc [dbo].[Suppliers_Delete]
@SupplierID int,
@UserDeleteID int
as
update Suppliers set 
SupplierState = 0
where SupplierID = @SupplierID
		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف المورد '+(select SupplierName from Suppliers where      SupplierID=@SupplierID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() ) 


