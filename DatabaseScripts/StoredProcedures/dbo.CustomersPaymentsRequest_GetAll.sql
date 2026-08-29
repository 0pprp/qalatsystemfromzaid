CREATE PROC [dbo].[CustomersPaymentsRequest_GetAll]
    @CustomerName NVARCHAR(100) = NULL,
    @DelegateID INT = NULL
AS
BEGIN
    SELECT * 
    FROM View_CustomersPaymentsRequestFinal
    WHERE 
        (@CustomerName IS NULL OR CustomerName LIKE N'%' + @CustomerName + N'%')
        AND (@DelegateID IS NULL OR DelegateID = @DelegateID)
END


