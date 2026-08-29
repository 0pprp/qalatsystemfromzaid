

CREATE procEDURE [dbo].[InsertServerTransferStores]
    @FromStoreID INT = NULL,
    @ToStoreID INT = NULL,
    @UserID INT = NULL,
    @TransferStoreDate DATETIME= NULL,
    @State BIT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[TransferStores]
           ([FromStoreID]
           ,[ToStoreID]
           ,[UserID]
           ,[TransferStoreDate]
           ,[State]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@FromStoreID
           ,@ToStoreID
           ,@UserID
           ,@TransferStoreDate
           ,@State
           ,@AsyncState
           ,@AsyncID)
END

