 CREATE proc [dbo].[GetCustomerNotZero]
 as
 select * from View_CustomersDelegate where 
   AmountRemaining >0

