CREATE proc [dbo].[GetCustomerSaleAsyncID]
@CustomerSaleID int = null
as
select AsyncID from CustomersSales where CustomerSaleID=@CustomerSaleID

