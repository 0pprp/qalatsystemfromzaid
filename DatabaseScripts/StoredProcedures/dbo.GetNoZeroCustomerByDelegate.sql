

 CREATE proc [dbo].[GetNoZeroCustomerByDelegate]
 @DelegateID int =null
 as
   select * from View_CustomersDelegate

where CustomerState='true' and DelegateID=@DelegateID 
and AmountRemaining>0 and IsLegal='false'


