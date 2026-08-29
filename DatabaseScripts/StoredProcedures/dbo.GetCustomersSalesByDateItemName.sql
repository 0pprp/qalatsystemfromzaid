CREATE proc [dbo].[GetCustomersSalesByDateItemName]
@FromDate datetime ,
@ToDate datetime, 
@ItemName nvarchar(255)
as
select * from View_CustomersSales
where 
CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate and 
ItemsNames like N'%'+@ItemName+N'%'

