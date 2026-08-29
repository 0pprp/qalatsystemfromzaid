
CREATE procEDURE [dbo].[InsertServerBuys]
    @UserID INT = NULL,
    @SupplierID INT = NULL,
    @BoundNumber INT = NULL,
    @Recipient NVARCHAR(255)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @DateCreate DATETIME NULL,
    @DateModify datetime =NULL,
    @StoreID INT = NULL,
    @BuyState BIT = NULL,
    @BoxID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Buys]
           ([UserID]
           ,[SupplierID]
           ,[BoundNumber]
           ,[Recipient]
           ,[Notes]
           ,[DateCreate]
           ,[DateModify]
           ,[StoreID]
           ,[BuyState]
           ,[BoxID]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@SupplierID
           ,@BoundNumber
           ,@Recipient
           ,@Notes
           ,@DateCreate
           ,@DateModify
           ,@StoreID
           ,@BuyState
           ,@BoxID
           ,@AsyncState
           ,@AsyncID)
END

