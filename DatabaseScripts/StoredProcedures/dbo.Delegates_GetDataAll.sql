CREATE proc [dbo].[Delegates_GetDataAll]
AS
BEGIN
    SELECT * FROM Delegates where DelegateState='true';
END


