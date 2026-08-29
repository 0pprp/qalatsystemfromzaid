CREATE proc [dbo].[AsyncStateUpdateCustomersSale]
@CustomerSaleID int =null 
as
update CustomersSales set AsyncState='true' where CustomerSaleID=@CustomerSaleID

