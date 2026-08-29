CREATE proc [dbo].[GetAllCustomerWeekPaymentDeviceDelegate]
@DelegateID int = NULL
as
select * from View_CustomerWeekPaymentDevice 
where DelegateID=@DelegateID and AmountRemaining>0

