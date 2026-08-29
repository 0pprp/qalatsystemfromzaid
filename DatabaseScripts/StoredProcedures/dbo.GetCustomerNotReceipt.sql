 CREATE proc [dbo].[GetCustomerNotReceipt]
 as
 select * from View_CustomersDelegate where AmountTotalSales >0 and  AmountDaySales>0 and ReceiptsTotal=0

