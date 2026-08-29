CREATE proc [dbo].[GetCustomerSaleByID]
@CustomerSaleID int = NULL
as
select * from CustomersSales where CustomerSaleID=@CustomerSaleID

