 
CREATE proc [dbo].[GetCustomerIsLegalByCustomerName]
@CustomerName nvarchar(255)
as
select * from View_CustomersDelegate 
where CustomerState='true'  and IsLegal='true' and CustomerName like N'%'+@CustomerName+N'%'

