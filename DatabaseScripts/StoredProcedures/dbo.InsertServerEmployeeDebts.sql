
CREATE procEDURE [dbo].[InsertServerEmployeeDebts]
    @UserID INT = NULL,
    @EmployeeID INT = NULL,
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

    INSERT INTO [dbo].[EmployeeDebts]
           ([UserID]
           ,[EmployeeID]
           ,[AmountDebt]
           ,[DateDebt]
           ,[Purpose]
           ,[Notes]
           ,[AccountType]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@EmployeeID
           ,@AmountDebt
           ,@DateDebt
           ,@Purpose
           ,@Notes
           ,@AccountType
           ,@AsyncState
           ,@AsyncID)
END

