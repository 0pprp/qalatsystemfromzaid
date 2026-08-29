 
CREATE proc [dbo].[GetCustomersSalesCustomerName]
@CustomerName nvarchar(100),
@DelegateID int
as
select * from View_CustomersSales where DelegateID=@DelegateID and CustomerName like N''+@CustomerName+N'%'

