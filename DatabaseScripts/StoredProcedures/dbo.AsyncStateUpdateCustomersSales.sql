
CREATE proc [dbo].[AsyncStateUpdateCustomersSales]
@CustomerSaleID int = NULL
as
update CustomersSales set AsyncState='true' where CustomerSaleID=@CustomerSaleID

