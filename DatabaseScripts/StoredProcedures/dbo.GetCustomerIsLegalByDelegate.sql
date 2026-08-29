CREATE proc [dbo].[GetCustomerIsLegalByDelegate]
@DelegateID int = NULL
as
select * from View_CustomersDelegate 
where CustomerState='true' and DelegateID=@DelegateID and IsLegal='true'

