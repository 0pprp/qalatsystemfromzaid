 
CREATE proc [dbo].[GetAllCustomerMonthPaymentDeviceDelegate]
@DelegateID int = NULL
as
select * from View_CustomerMonthPaymentDevice 
where DelegateID=@DelegateID and AmountRemaining>0

