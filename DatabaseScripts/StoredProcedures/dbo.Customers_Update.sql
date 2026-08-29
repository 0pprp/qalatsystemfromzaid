


CREATE PROCEDURE [dbo].[Customers_Update]
    @CustomerID INT,
    @UserUpdateID INT = NULL,
    @CustomerName NVARCHAR(100) = NULL,
    @PhoneNumber NVARCHAR(100) = NULL,
    @Address NVARCHAR(100) = NULL,
    @ShopName NVARCHAR(100) = NULL,
    @SaleName NVARCHAR(100) = NULL,
    @Notes NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Customers
    SET 
        UserID = @UserUpdateID,
        CustomerName = @CustomerName,
        PhoneNumber = @PhoneNumber,
        Address = @Address,
        ShopName = @ShopName,
        SaleName = @SaleName,
        Notes = @Notes
    WHERE CustomerID = @CustomerID;
		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل العميل  '+@CustomerName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
	select * from View_CustomersDelegate where CustomerID=@CustomerID
END


