CREATE proc [dbo].[CustomersSaleAllData]
as
select CustomerSaleID, CustomerID,DateCreate,DiscountAmountTotal,DiscountAmountTotalDay,AsyncID from CustomersSales

