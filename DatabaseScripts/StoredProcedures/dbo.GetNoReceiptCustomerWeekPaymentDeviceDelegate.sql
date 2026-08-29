CREATE proc [dbo].[GetNoReceiptCustomerWeekPaymentDeviceDelegate]
@DelegateID int = NULL
as
select * from View_CustomerWeekPaymentDevice 
where DelegateID=@DelegateID and AmountRemaining>0 and
Amount1=0 and
Amount2=0 and
Amount3=0 and
Amount4=0 and
Amount5=0 and
Amount6=0 and
Amount7=0 

