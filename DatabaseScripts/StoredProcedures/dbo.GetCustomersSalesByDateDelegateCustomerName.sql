CREATE proc [dbo].[GetCustomersSalesByDateDelegateCustomerName]
@FromDate datetime ,
@ToDate datetime,
@DelegateID int = NULL,
@CustomerName nvarchar(255)
as
select * from View_CustomersSales
where 
CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate and
DelegateID=@DelegateID and
CustomerName like N'%'+@CustomerName+N'%' or ItemsNames like N'%'+@CustomerName+N'%'

