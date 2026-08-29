CREATE proc [dbo].[GetNoReceiptCustomerMonthPaymentDeviceDelegate]
@DelegateID int = NULL
as
select * from View_CustomerMonthPaymentDevice 
where DelegateID=@DelegateID and AmountRemaining>0 and
Amount1=0 and
Amount2=0 and
Amount3=0 and
Amount4=0 and
Amount5=0 and
Amount6=0 and
Amount7=0 and
Amount8=0 and
Amount9=0 and
Amount10=0 and
Amount11=0 and
Amount12=0 and
Amount13=0 and
Amount14=0 and
Amount15=0 and
Amount16=0 and
Amount17=0 and
Amount18=0 and
Amount19=0 and
Amount20=0 and
Amount21=0 and
Amount22=0 and
Amount23=0 and
Amount24=0 and
Amount25=0 and
Amount26=0 and
Amount27=0 and
Amount28=0 and
Amount29=0 and
Amount30=0  

