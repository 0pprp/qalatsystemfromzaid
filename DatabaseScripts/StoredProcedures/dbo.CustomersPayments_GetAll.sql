CREATE proc [dbo].[CustomersPayments_GetAll]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @DelegateID INT = NULL,
    @TextSearch NVARCHAR(255) = NULL
AS
BEGIN
    SELECT * 
    FROM View_CustomersPayments 
    WHERE 
        (@FromDate IS NULL OR CONVERT(DATE, PaymentDate) >= CONVERT(DATE, @FromDate))
        AND (@ToDate IS NULL OR CONVERT(DATE, PaymentDate) <= CONVERT(DATE, @ToDate))
        AND (@DelegateID IS NULL OR DelegateID = @DelegateID)
        AND (@TextSearch IS NULL 
             OR CustomerName LIKE N'%' + @TextSearch + N'%'
             OR PhoneNumber LIKE N'%' + @TextSearch + N'%');
END


