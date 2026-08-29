CREATE proc [dbo].[DeleteSuppliersAccount]
@SupplierAccountID int = NULL,
@UserID int = NULL
as
exec DeleteOnSuppliersAccountsAsyncID @SupplierAccountID=@SupplierAccountID

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف من دين المورد '+(select SupplierName from View_SuppliersAccounts where SupplierAccountID=@SupplierAccountID)+N' المبلغ '+(select SupplierName from View_SuppliersAccounts where SupplierAccountID=@SupplierAccountID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from SuppliersAccounts
where SupplierAccountID=@SupplierAccountID

 

