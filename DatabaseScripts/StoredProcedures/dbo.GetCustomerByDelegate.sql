 CREATE proc [dbo].[GetCustomerByDelegate]
 @DelegateID int =null
 as
   select * from View_CustomersDelegate

where CustomerState='true' and DelegateID=@DelegateID  order by CustomerID

