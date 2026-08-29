
-- ثانياً: إنشاء الإجراء المخزن الجديد الذي يستقبل الجدول
CREATE proc [dbo].[PostSelectPaymentCustomerTemporaryMulti]
@PaymentData [dbo].[CustomersPaymentsRequestType] READONLY
as
BEGIN
    INSERT INTO CustomersPaymentsRequest (CustomerID, DelegateID, Amount, Location, PaymentDate) 
    SELECT CustomerID, DelegateID, Amount, Location, GETDATE() 
    FROM @PaymentData
END

