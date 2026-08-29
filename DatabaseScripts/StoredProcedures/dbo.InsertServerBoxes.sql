
CREATE procEDURE [dbo].[InsertServerBoxes]
    @BoxName NVARCHAR(255) = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @BoxState BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Boxes]
           ([BoxName]
           ,[AsyncState]
           ,[AsyncID]
           ,[BoxState])
     VALUES
           (@BoxName
           ,@AsyncState
           ,@AsyncID
           ,@BoxState)
END

