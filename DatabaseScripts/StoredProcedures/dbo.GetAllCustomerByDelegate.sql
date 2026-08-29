
 CREATE proc [dbo].[GetAllCustomerByDelegate]
 @DelegateID int =null
 as
   select * from View_Customers

where CustomerState='true' and DelegateID=@DelegateID  and AmountRemaining>0 

