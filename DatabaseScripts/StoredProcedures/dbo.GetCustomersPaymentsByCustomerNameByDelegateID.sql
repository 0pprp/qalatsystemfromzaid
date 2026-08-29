 
CREATE proc [dbo].[GetCustomersPaymentsByCustomerNameByDelegateID]
@CustomerName nvarchar(255),
@DelegateID int
as
select * from View_CustomersPayments
where DelegateID=@DelegateID and CustomerName like N''+@CustomerName+N'%' order by CustomerPaymentID

