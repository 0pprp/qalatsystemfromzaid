CREATE proc [dbo].[GetSupplierAccountType]
@SupplierID int = NULL,
@AccountType nvarchar(255)
as
select * from View_SuppliersAccounts
where 
SupplierID=@SupplierID  and AccountType=@AccountType

