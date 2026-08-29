CREATE proc [dbo].[DeleteOnSuppliersAccountsAsyncID]
@SupplierAccountID int = NULL
as
insert into DeleteData (SuppliersAccountsAsyncID) values ((select AsyncID from SuppliersAccounts where SupplierAccountID=@SupplierAccountID))

