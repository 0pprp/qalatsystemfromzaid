 CREATE proc [dbo].[GetCustomerZero]
 as
 select * from View_CustomersDelegate where 
 AmountRemaining =0

