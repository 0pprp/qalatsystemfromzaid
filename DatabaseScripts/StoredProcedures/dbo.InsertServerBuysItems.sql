
CREATE procEDURE [dbo].[InsertServerBuysItems]
    @UserID INT = NULL,
    @BuyID INT = NULL,
    @ItemID INT = NULL,
    @Quantity INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[BuysItems]
           ([UserID]
           ,[BuyID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@BuyID
           ,@ItemID
           ,@Quantity
           ,@AsyncState
           ,@AsyncID)
END

