CREATE proc [dbo].[GetReceiptCustomerWeekPaymentDeviceDelegate]
@DelegateID int = NULL
as
select * from View_CustomerWeekPaymentDevice 
where DelegateID=@DelegateID and AmountRemaining>0 and
Amount1>0 or
Amount2>0 or
Amount3>0 or
Amount4>0 or
Amount5>0 or
Amount6>0 or
Amount7>0 

