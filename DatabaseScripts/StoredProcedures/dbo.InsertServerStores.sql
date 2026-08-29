

CREATE procEDURE [dbo].[InsertServerStores]
    @UserID INT = NULL,
    @StoreName NVARCHAR(255)= NULL,
    @StorePlace NVARCHAR(255)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @CityID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @State BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Stores]
           ([UserID]
           ,[StoreName]
           ,[StorePlace]
           ,[Notes]
           ,[CityID]
           ,[AsyncState]
           ,[AsyncID]
           ,[State])
     VALUES
           (@UserID
           ,@StoreName
           ,@StorePlace
           ,@Notes
           ,@CityID
           ,@AsyncState
           ,@AsyncID
           ,@State)
END

