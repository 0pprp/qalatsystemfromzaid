create proc [dbo].[GetCustomersSalesByCustomerByDelegateID]
 @DelegateID int =null,
 @CustomerName nvarchar(100) =null
 as
 select * from View_CustomersSales
 where DelegateID=@DelegateID and CustomerName like N'%'+@CustomerName+N'%'

