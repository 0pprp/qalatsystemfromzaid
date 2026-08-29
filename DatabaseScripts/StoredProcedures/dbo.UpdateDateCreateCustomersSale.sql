CREATE proc [dbo].[UpdateDateCreateCustomersSale]
@CustomerSaleID int = NULL,
@DateCreate datetime 
as
update CustomersSales set DateCreate=@DateCreate where CustomerSaleID=@CustomerSaleID

