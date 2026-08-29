
CREATE proc [dbo].[AsyncStateUpdateSuppliersAccounts]
@SupplierAccountID int = NULL
as
update SuppliersAccounts set AsyncState='true' where SupplierAccountID=@SupplierAccountID

