CREATE proc [dbo].[UpdateDiscountAmountTotalDayCustomersSale]
@CustomerSaleID int = NULL,
@DiscountAmountTotalDay float 
as
update CustomersSales set DiscountAmountTotalDay=@DiscountAmountTotalDay where CustomerSaleID=@CustomerSaleID

