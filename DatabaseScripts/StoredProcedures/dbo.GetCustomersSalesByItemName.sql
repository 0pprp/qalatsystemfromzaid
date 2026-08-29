 CREATE proc [dbo].[GetCustomersSalesByItemName]
 @ItemName nvarchar(255) 
 as
 select * from View_CustomersSales
 where ItemsNames like N'%'+@ItemName+N'%'

