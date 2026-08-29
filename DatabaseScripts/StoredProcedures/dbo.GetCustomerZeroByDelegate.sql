 CREATE proc [dbo].[GetCustomerZeroByDelegate]
 @DelegateID int =null
 as
 select * from View_CustomersDelegate where  DelegateID=1 and AmountRemaining =0

