

CREATE procEDURE [dbo].[InsertServerCustomers]
    @DelegateID INT = NULL,
    @UserID INT = NULL,
    @CityID INT = NULL,
    @CustomerName NVARCHAR(255)= NULL,
    @Address NVARCHAR(255)= NULL,
    @Longitude FLOAT= NULL,
    @Latitude FLOAT= NULL,
    @CustomerImage NVARCHAR(MAX)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @PhoneNumber NVARCHAR(255)= NULL,
    @CustomerState BIT = NULL,
    @ShopName NVARCHAR(255)= NULL,
    @StoreAddress NVARCHAR(255)= NULL,
    @NearestFunctionPoint NVARCHAR(255)= NULL,
    @StorePhoneNumber NVARCHAR(255)= NULL,
    @Neighborhood NVARCHAR(255)= NULL,
    @AmountReceverDay FLOAT= NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @SelectState BIT = NULL,
    @SaleName NVARCHAR(255)= NULL,
    @ReceiptName NVARCHAR(255)= NULL,
    @IsLegal BIT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Customers]
           ([DelegateID]
           ,[UserID]
           ,[CityID]
           ,[CustomerName]
           ,[Address]
           ,[Longitude]
           ,[Latitude]
           ,[CustomerImage]
           ,[Notes]
           ,[PhoneNumber]
           ,[CustomerState]
           ,[ShopName]
           ,[StoreAddress]
           ,[NearestFunctionPoint]
           ,[StorePhoneNumber]
           ,[Neighborhood]
           ,[AmountReceverDay]
           ,[AsyncState]
           ,[AsyncID]
           ,[SelectState]
           ,[SaleName]
           ,[ReceiptName]
           ,[IsLegal])
     VALUES
           (@DelegateID
           ,@UserID
           ,@CityID
           ,@CustomerName
           ,@Address
           ,@Longitude
           ,@Latitude
           ,@CustomerImage
           ,@Notes
           ,@PhoneNumber
           ,@CustomerState
           ,@ShopName
           ,@StoreAddress
           ,@NearestFunctionPoint
           ,@StorePhoneNumber
           ,@Neighborhood
           ,@AmountReceverDay
           ,@AsyncState
           ,@AsyncID
           ,@SelectState
           ,@SaleName
           ,@ReceiptName
           ,@IsLegal)
END

