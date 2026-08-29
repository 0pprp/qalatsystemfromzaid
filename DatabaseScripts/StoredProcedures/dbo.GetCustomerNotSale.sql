 CREATE proc [dbo].[GetCustomerNotSale]
 as
  select * from View_CustomersDelegate where AmountTotalSales =0 and 
							      AmountDaySales=0

