CREATE proc [dbo].[GetCustomersDelegateAll]
@DelegateID int
as
select * from View_Customers where DelegateID=@DelegateID and AmountRemaining>0

