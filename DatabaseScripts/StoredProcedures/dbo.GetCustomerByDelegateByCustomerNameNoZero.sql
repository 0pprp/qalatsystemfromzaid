CREATE proc [dbo].[GetCustomerByDelegateByCustomerNameNoZero]
@DelegateID int = NULL,
@CustomerName nvarchar(255)
as
select * from View_CustomersDelegate 
where CustomerState='true' and DelegateID=@DelegateID and AmountRemaining>0  and CustomerName like N'%'+@CustomerName+N'%' order by CustomerID

