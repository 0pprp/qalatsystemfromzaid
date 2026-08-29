CREATE proc [dbo].[DeleteCustomerPaymentRequestSame]
as
WITH NumberedPayments AS (
    SELECT
        CustomersPaymentsRequestID,
        CustomerID,
        Amount,
		PaymentDate,
        ROW_NUMBER() OVER (PARTITION BY CustomerID, Amount,CONVERT(date, PaymentDate) ORDER BY CustomersPaymentsRequestID) AS RowNum
    FROM
        CustomersPaymentsRequest
)
DELETE FROM NumberedPayments
WHERE RowNum > 1;

