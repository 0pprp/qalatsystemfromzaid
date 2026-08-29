CREATE proc [dbo].[Customers_GetAll]
    @DelegateID INT = NULL,
    @TextSearch NVARCHAR(255) = NULL,
    @ShowType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM View_CustomersDelegate
    WHERE 
        (@DelegateID IS NULL OR DelegateID = @DelegateID)
        AND (@TextSearch IS NULL 
             OR CustomerName LIKE N'%' + @TextSearch + N'%'
             OR PhoneNumber LIKE N'%' + @TextSearch + N'%')
        AND CustomerState = 1
        
        AND (
            (@ShowType = N'الجميع' AND AmountRemaining >= 0)
            
            OR (@ShowType = N'الغير مصفرين' AND AmountRemaining > 0)
            
            OR (@ShowType = N'المصفرين' AND AmountRemaining = 0)
            
            -- استخدام ISNULL لضمان دخول القيم الفارغة في الحساب
            OR (@ShowType = N'القانونية' AND AmountRemaining > 0 AND IsLegal = 'true')
            
            OR (@ShowType = N'المستمرين' AND AmountRemaining > 0 
                AND ISNULL(IsLegal, 0) = 0 
                AND (LastPaymentDate > DATEADD(YEAR, -1, GETDATE()) OR DateSaleDevice > DATEADD(YEAR, -1, GETDATE())))
            
            OR (@ShowType = N'المتوقفين' AND AmountRemaining > 0 
                AND ISNULL(IsLegal, 0) = 0 
                AND (ISNULL(LastPaymentDate, '1900-01-01') <= DATEADD(YEAR, -1, GETDATE()) 
                     AND ISNULL(DateSaleDevice, '1900-01-01') <= DATEADD(YEAR, -1, GETDATE())))
        )
END
