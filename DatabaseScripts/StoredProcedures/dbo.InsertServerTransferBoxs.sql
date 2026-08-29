
CREATE procEDURE [dbo].[InsertServerTransferBoxs]
    @FromBoxID INT = NULL,
    @ToBoxID INT = NULL,
    @UserID INT = NULL,
    @Amount FLOAT= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @DateModify datetime =NULL,
    @DateCreate DATETIME = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[TransferBoxs]
           ([FromBoxID]
           ,[ToBoxID]
           ,[UserID]
           ,[Amount]
           ,[Notes]
           ,[DateModify]
           ,[DateCreate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@FromBoxID
           ,@ToBoxID
           ,@UserID
           ,@Amount
           ,@Notes
           ,@DateModify
           ,@DateCreate
           ,@AsyncState
           ,@AsyncID)
END

