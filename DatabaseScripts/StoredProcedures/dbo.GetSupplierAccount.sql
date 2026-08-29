CREATE proc [dbo].[GetSupplierAccount]
@SupplierID int = NULL
as
select * from View_SuppliersAccounts
where 
SupplierID=@SupplierID 

