CREATE proc [dbo].[CustomersNoSalesFinal]
as
select CustomerID,CustomerName,AsyncID from GetCustomersAmountTotalSalesAndReceiptsTotal where AmountTotalSales=0 and ReceiptsTotal=0

