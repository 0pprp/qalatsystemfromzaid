

CREATE procEDURE [dbo].[InsertServerItems]
    @StoreID INT = NULL,
    @UserID INT = NULL,
    @ItemName NVARCHAR(255)= NULL,
    @ItemPrice FLOAT= NULL,
    @ItemCost FLOAT= NULL,
    @Quantity INT = NULL,
    @ItemImage NVARCHAR(MAX)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @NotificationNumber INT = NULL,
    @AmountDay FLOAT= NULL,
    @NumberOfSales INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @Link NVARCHAR(MAX)= NULL,
    @ItemState BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Items]
           ([StoreID]
           ,[UserID]
           ,[ItemName]
           ,[ItemPrice]
           ,[ItemCost]
           ,[Quantity]
           ,[ItemImage]
           ,[Notes]
           ,[NotificationNumber]
           ,[AmountDay]
           ,[NumberOfSales]
           ,[AsyncState]
           ,[AsyncID]
           ,[Link]
           ,[ItemState])
     VALUES
           (@StoreID
           ,@UserID
           ,@ItemName
           ,@ItemPrice
           ,@ItemCost
           ,@Quantity
           ,@ItemImage
           ,@Notes
           ,@NotificationNumber
           ,@AmountDay
           ,@NumberOfSales
           ,@AsyncState
           ,@AsyncID
           ,@Link
           ,@ItemState)
END

