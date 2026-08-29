CREATE proc [dbo].[UpdateCustomerSalesBalance]
@CustomerSaleBalanceID int = NULL,
@DateCreate datetime,
@DiscountAmountTotal float,
@DiscountAmountTotalDay float
as 
update CustomerSaleBalance set DiscountAmountTotal=@DiscountAmountTotal ,
DateCreate=@DateCreate,
DiscountAmountTotalDay=@DiscountAmountTotalDay
where CustomerSaleBalanceID=@CustomerSaleBalanceID

