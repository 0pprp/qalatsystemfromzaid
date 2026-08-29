CREATE PROCEDURE [dbo].[Delegates_GetAll]
@DelegateName NVARCHAR(100) = NULL
AS
BEGIN
    SELECT * 
    FROM View_Delegates 
    WHERE DelegateState = 'true'
    AND (@DelegateName IS NULL OR DelegateName LIKE '%' + @DelegateName + '%');
END;


