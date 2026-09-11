CREATE OR ALTER PROC [dbo].[Customers_GetCustomerNotes]
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        N.NoteID,
        N.CustomerID,
        N.UserID,
        N.NoteText,
        CAST(COALESCE(N.CreatedAtUtc, N.CreatedDate) AS DATETIME) AS CreatedDate,
        COALESCE(NULLIF(LTRIM(RTRIM(N.CreatedByName)), N''), U.UserName, N'—') AS UserName,
        U.UserType
    FROM dbo.CustomerNotes N
    LEFT JOIN dbo.Users U ON U.UserID = N.UserID
    WHERE N.CustomerID = @CustomerID
    ORDER BY COALESCE(N.CreatedAtUtc, N.CreatedDate) DESC;
END
