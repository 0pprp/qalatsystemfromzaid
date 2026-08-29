

CREATE procEDURE [dbo].[InsertServerSelectDelegate]
    @DelegateFatherID INT = NULL,
    @DelegateChildID INT = NULL,
    @UserID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[SelectDelegate]
           ([DelegateFatherID]
           ,[DelegateChildID]
           ,[UserID]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@DelegateFatherID
           ,@DelegateChildID
           ,@UserID
           ,@AsyncState
           ,@AsyncID)
END

