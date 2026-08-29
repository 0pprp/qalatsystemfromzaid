CREATE proc [dbo].[GetReceiptCustomerMonthPaymentDeviceDelegate]
@DelegateID int = NULL
as
select * from View_CustomerMonthPaymentDevice 
where DelegateID=@DelegateID and AmountRemaining>0 and
Amount1>0  or
Amount2>0  or
Amount3>0  or
Amount4>0  or
Amount5>0  or
Amount6>0  or
Amount7>0  or
Amount8>0  or
Amount9>0  or
Amount10>0 or
Amount11>0 or
Amount12>0 or
Amount13>0 or
Amount14>0 or
Amount15>0 or
Amount16>0 or
Amount17>0 or
Amount18>0 or
Amount19>0 or
Amount20>0 or
Amount21>0 or
Amount22>0 or
Amount23>0 or
Amount24>0 or
Amount25>0 or
Amount26>0 or
Amount27>0 or
Amount28>0 or
Amount29>0 or
Amount30>0  

