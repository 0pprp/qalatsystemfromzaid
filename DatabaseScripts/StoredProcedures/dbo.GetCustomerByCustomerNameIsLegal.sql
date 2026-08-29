CREATE proc [dbo].[GetCustomerByCustomerNameIsLegal]
@CustomerName nvarchar(255) = null 
as
select * from View_CustomersDelegate where   IsLegal='true' and
CustomerName like N'%'+@CustomerName+N'%'

