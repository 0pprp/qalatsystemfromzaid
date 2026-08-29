CREATE proc [dbo].[GetSelectItemsSalesByCustomersSales]
@CustomerSaleID int = NULL
as
select * from View_SelectItemsSales
where CustomerSaleID=@CustomerSaleID

