create proc [dbo].[GetCustomersByDelegatePermissions]
@DelegateID int
as
select * from View_Customers where AmountRemaining>0 and exists
(
select * from SelectDelegate where SelectDelegate.DelegateChildID=View_Customers.DelegateID and SelectDelegate.DelegateFatherID=@DelegateID
)

