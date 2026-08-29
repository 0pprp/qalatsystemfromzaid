CREATE proc [dbo].[CheckCustomerFind]
@DelegateID int = NULL,
@CustomerName nvarchar(255)
as
select * from Customers where DelegateID=@DelegateID and CustomerName=@CustomerName

