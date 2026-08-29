CREATE proc [dbo].[GetView_CustomersDelegateLegalByDelegateID]
@DelegateID int = NULL
as
select * from View_CustomersDelegate where DelegateID=@DelegateID and IsLegal='true'

