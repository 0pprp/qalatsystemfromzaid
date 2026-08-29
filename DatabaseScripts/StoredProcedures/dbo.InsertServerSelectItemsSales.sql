

CREATE procEDURE [dbo].[InsertServerSelectItemsSales]
    @UserID INT = NULL,
    @CustomerSaleID INT = NULL,
    @ItemID INT = NULL,
    @Quantity INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[SelectItemsSales]
           ([UserID]
           ,[CustomerSaleID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@CustomerSaleID
           ,@ItemID
           ,@Quantity
           ,@AsyncState
           ,@AsyncID)
END

