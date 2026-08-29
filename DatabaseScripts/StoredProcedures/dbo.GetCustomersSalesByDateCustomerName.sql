CREATE proc [dbo].[GetCustomersSalesByDateCustomerName]
@FromDate datetime ,
@ToDate datetime, 
@CustomerName nvarchar(255)
as
select * from View_CustomersSales
where 
CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate and 
CustomerName like N'%'+@CustomerName+N'%' or ItemsNames like N'%'+@CustomerName+N'%'

