 
 CREATE proc [dbo].[GetAllCustomerByDelegateByCustomerName]
 @DelegateID int = NULL,
 @CustomerName nvarchar(255)
 as
select * from View_CustomersDelegate
where CustomerState='true' and DelegateID=@DelegateID and CustomerName like N'%'+@CustomerName+N'%' or SaleName like N'%'+@CustomerName+N'%'
 

