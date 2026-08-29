CREATE PROCEDURE [dbo].[Users_GetAll]
    @TextSearch NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * 
    FROM Users 
    WHERE UserState = 1 
        AND (@TextSearch IS NULL OR 
             UserName LIKE '%' + @TextSearch + '%' OR 
             Email LIKE '%' + @TextSearch + '%' OR 
             PhoneNumber LIKE '%' + @TextSearch + '%');
END;


