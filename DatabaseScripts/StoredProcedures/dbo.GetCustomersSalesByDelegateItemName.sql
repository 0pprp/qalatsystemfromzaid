CREATE proc [dbo].[GetCustomersSalesByDelegateItemName]
@ItemName nvarchar(255),
@DelegateID int = NULL
as
select * from View_CustomersSales
where 
DelegateID=@DelegateID and
ItemsNames like N'%'+@ItemName+N'%'

