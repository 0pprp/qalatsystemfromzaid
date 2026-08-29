CREATE proc [dbo].[GetCustomersSalesByDateDelegateItemName]
@FromDate datetime ,
@ToDate datetime,
@DelegateID int = NULL,
@ItemName nvarchar(255)
as
select * from View_CustomersSales
where 
CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate and
DelegateID=@DelegateID and
ItemsNames like N'%'+@ItemName+N'%'

