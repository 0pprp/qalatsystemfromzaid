CREATE proc [dbo].[GetCustomersByItemsNames]
@ItemsNames nvarchar(255)
as
select * from View_CustomersDelegate 
where CustomerState='true' and ItemsNames like N'%'+@ItemsNames+N'%'

