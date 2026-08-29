CREATE proc [dbo].[GetCustomersPaymenByDelegateCustomerName]
@DelegateID int = NULL,
@CustomerName nvarchar(255)
as
select * from View_CustomersPayments
where DelegateID=@DelegateID and
CustomerName like  N'%'+@CustomerName+N'%'

