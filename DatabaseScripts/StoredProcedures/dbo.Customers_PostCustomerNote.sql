CREATE OR ALTER PROC [dbo].[Customers_PostCustomerNote]
    @CustomerID INT,
    @UserID INT = NULL,
    @NoteText NVARCHAR(MAX),
    @CreatedByName NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CustomerNotes (CustomerID, UserID, NoteText, CreatedByName, CreatedAtUtc, CreatedDate)
    VALUES (@CustomerID, @UserID, @NoteText, @CreatedByName, SYSUTCDATETIME(), GETDATE());

    DECLARE @NoteID INT = SCOPE_IDENTITY();

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
    WHERE N.NoteID = @NoteID;
END
