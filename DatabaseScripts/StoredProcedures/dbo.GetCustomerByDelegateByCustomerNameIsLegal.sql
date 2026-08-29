 
CREATE proc [dbo].[GetCustomerByDelegateByCustomerNameIsLegal]
@DelegateID int = null ,
@CustomerName nvarchar(255) = null 
as
select * from View_CustomersDelegate where DelegateID=@DelegateID and  IsLegal='true' and
CustomerName like N'%'+@CustomerName+N'%'

