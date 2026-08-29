CREATE proc [dbo].[UpdateShopNameCustomer]
@CustomerID int = NULL,
@ShopName nvarchar(255)
as
update Customers set ShopName=@ShopName where CustomerID=@CustomerID

