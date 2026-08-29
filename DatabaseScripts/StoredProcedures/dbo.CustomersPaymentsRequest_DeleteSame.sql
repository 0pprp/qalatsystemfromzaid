create proc [dbo].[CustomersPaymentsRequest_DeleteSame]
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

