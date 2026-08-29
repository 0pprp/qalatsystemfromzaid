

CREATE procEDURE [dbo].[InsertServerSuppliers]
    @UserID INT = NULL,
    @CityID INT = NULL,
    @SupplierName NVARCHAR(255)= NULL,
    @Address NVARCHAR(255)= NULL,
    @Longitude FLOAT= NULL,
    @Latitude FLOAT= NULL,
    @PhoneNumber NVARCHAR(255)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @SupplierImage NVARCHAR(MAX)= NULL,
    @SupplierState BIT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Suppliers]
           ([UserID]
           ,[CityID]
           ,[SupplierName]
           ,[Address]
           ,[Longitude]
           ,[Latitude]
           ,[PhoneNumber]
           ,[Notes]
           ,[SupplierImage]
           ,[SupplierState]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@CityID
           ,@SupplierName
           ,@Address
           ,@Longitude
           ,@Latitude
           ,@PhoneNumber
           ,@Notes
           ,@SupplierImage
           ,@SupplierState
           ,@AsyncState
           ,@AsyncID)
END

