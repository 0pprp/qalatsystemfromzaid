CREATE proc [dbo].[GetCustomersSalesByDelegateCustomerName]
@CustomerName nvarchar(255),
@DelegateID int = NULL
as
select * from View_CustomersSales
where 
DelegateID=@DelegateID and
CustomerName like N'%'+@CustomerName+N'%'

