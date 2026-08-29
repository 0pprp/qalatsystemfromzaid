CREATE OR ALTER PROC [dbo].[Customers_PostCustomerNote]
    @CustomerID INT,
    @UserID INT,
    @NoteText NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO CustomerNotes (CustomerID, UserID, NoteText, CreatedDate)
    VALUES (@CustomerID, @UserID, @NoteText, GETDATE());

    DECLARE @NoteID INT = SCOPE_IDENTITY();

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
    WHERE N.NoteID = @NoteID;
END
