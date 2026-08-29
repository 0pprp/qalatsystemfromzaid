CREATE proc [dbo].[UpdateDiscountAmountTotalCustomersSale]
@CustomerSaleID int = NULL,
@DiscountAmountTotal float 
as
update CustomersSales set DiscountAmountTotal=@DiscountAmountTotal where CustomerSaleID=@CustomerSaleID

