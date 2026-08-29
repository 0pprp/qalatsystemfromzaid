CREATE proc [dbo].[DeleteCustomerPaymentRequestSameByDelegate]
@DelegateID int
as
WITH NumberedPayments AS (
    SELECT
        CustomersPaymentsRequestID,
        CustomerID,
        Amount,
		PaymentDate,
        ROW_NUMBER() OVER (PARTITION BY CustomerID, Amount,CONVERT(date, PaymentDate) ORDER BY CustomersPaymentsRequestID) AS RowNum
    FROM
        CustomersPaymentsRequest where DelegateID=@DelegateID
)
DELETE FROM NumberedPayments
WHERE RowNum > 1;

