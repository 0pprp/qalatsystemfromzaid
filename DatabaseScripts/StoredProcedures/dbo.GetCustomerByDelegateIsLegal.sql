CREATE proc [dbo].[GetCustomerByDelegateIsLegal]
@DelegateID int = null 
as
select * from View_CustomersDelegate where DelegateID=@DelegateID and  IsLegal='true'  

