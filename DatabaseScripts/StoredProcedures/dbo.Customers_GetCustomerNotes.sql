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
        N.CreatedDate,
        U.UserName,
        U.UserType
    FROM CustomerNotes N
    INNER JOIN Users U ON U.UserID = N.UserID
    WHERE N.CustomerID = @CustomerID
    ORDER BY N.CreatedDate DESC;
END
