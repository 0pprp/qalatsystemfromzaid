 CREATE proc [dbo].[GetCustomersSalesByDelegate]
 @DelegateID int =null
 as
 select * from View_CustomersSales
 where DelegateID=@DelegateID

