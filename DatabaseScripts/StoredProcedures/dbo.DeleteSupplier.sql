CREATE proc [dbo].[DeleteSupplier]
@SupplierID int = NULL,
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
           ,N'تم حذف المورد '+(select SupplierName from Suppliers where SupplierID=@SupplierID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
UPDATE [dbo].[Suppliers]
   SET  SupplierState = 'false'
 WHERE  SupplierID=@SupplierID

