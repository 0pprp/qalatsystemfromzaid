

CREATE procEDURE [dbo].[InsertServerDelegatesDebts]
    @UserID INT = NULL,
    @DelegateID INT = NULL,
    @AmountDebt FLOAT= NULL,
    @DateDebt DATETIME= NULL,
    @Purpose NVARCHAR(MAX)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @AccountType NVARCHAR(255)= NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[DelegatesDebts]
           ([UserID]
           ,[DelegateID]
           ,[AmountDebt]
           ,[DateDebt]
           ,[Purpose]
           ,[Notes]
           ,[AccountType]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@DelegateID
           ,@AmountDebt
           ,@DateDebt
           ,@Purpose
           ,@Notes
           ,@AccountType
           ,@AsyncState
           ,@AsyncID)
END

