CREATE proc [dbo].[GetDelegateSaleAndPaymentDate]
@FromDate datetime,
@ToDate datetime
as 
select DelegateID,CityID, DelegateName,
(select ISNULL(sum(AmountTotalSalesDenar),0) from View_CustomersSalesDelegate  where   CONVERT(date, DateCreate)>=@FromDate and CONVERT(date, DateCreate)<=@ToDate    and DelegateID=Delegates.DelegateID   )as AmountPrice,
(select ISNULL(sum(AmountDenar),0) from View_CustomersPaymentsDelegate  where          CONVERT(date, PaymentDate)>=@FromDate and CONVERT(date, PaymentDate)<=@ToDate  and DelegateID=Delegates.DelegateID          )as AmountReceipt from Delegates where DelegateState='true' 


