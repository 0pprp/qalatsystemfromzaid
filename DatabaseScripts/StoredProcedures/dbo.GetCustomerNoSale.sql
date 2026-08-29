CREATE proc [dbo].[GetCustomerNoSale]
as
select * from View_CustomersDelegate where AmountTotalSales=0 and ReceiptsTotal=0

