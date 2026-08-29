

CREATE procEDURE [dbo].[InsertServerSelectItemsWithdrawal]
    @WithdrawalStoresID INT = NULL,
    @UserID INT = NULL,
    @ItemID INT = NULL,
    @Quantity INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[SelectItemsWithdrawal]
           ([WithdrawalStoresID]
           ,[UserID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@WithdrawalStoresID
           ,@UserID
           ,@ItemID
           ,@Quantity
           ,@AsyncState
           ,@AsyncID)
END

