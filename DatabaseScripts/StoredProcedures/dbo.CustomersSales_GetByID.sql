create proc [dbo].[CustomersSales_GetByID]
@CustomerSaleID int
as
select * from View_CustomersSales where CustomerSaleID=@CustomerSaleID

