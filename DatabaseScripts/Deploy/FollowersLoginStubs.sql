CREATE OR ALTER PROC dbo.GetDelegateCheckLogout
    @AsyncID NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.View_Delegates WHERE AsyncID = @AsyncID;
END
GO

CREATE OR ALTER PROC dbo.GetDelegateTitle
    @DelegateID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.View_Delegates WHERE DelegateID = @DelegateID;
END
GO
