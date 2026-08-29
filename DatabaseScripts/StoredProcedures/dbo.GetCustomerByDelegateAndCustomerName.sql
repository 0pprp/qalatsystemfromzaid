 CREATE proc [dbo].[GetCustomerByDelegateAndCustomerName]
 @DelegateID int = NULL,
 @CustomerName nvarchar(255)
 as
 select * from View_CustomersDelegate where CustomerState='true'
and DelegateID=@DelegateID and CustomerName like N'%'+@CustomerName+N'%'

