 
CREATE PROCEDURE [dbo].[Delegates_Create]
    @DelegateName NVARCHAR(100),
    @UserCreateID INT,
    @Address NVARCHAR(100),
    @PhoneNumber NVARCHAR(100),
    @ReceiptName NVARCHAR(100),
    @AsyncID NVARCHAR(100) ,
    @Notes NVARCHAR(100)
AS
BEGIN
    IF @AsyncID IS NULL OR @AsyncID = ''
    BEGIN
        SET @AsyncID = NEWID();
    END
	declare @DBName nvarchar(100) =  DB_NAME()
	declare @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID)
	DECLARE @BoxName nvarchar(255) = N'خزينة '+@DelegateName+''
	exec InsertBox @BoxName =@BoxName,@UserID=@UserCreateID
	DECLARE @BoxID INT = (select top 1 BoxID from Boxes order by BoxID desc);
    INSERT INTO Delegates (DelegateName, CityID, UserID, Address, PhoneNumber, AsyncID, Notes,AsyncState,ReceiptName,DelegateState,BoxID)
    VALUES (@DelegateName, @CityID, @UserCreateID, @Address, @PhoneNumber, @AsyncID, @Notes,0,@ReceiptName,1,@BoxID);

   
	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة المندوب '+@DelegateName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
	DECLARE @LastId int;
	SET @LastId = IDENT_CURRENT('Delegates');
 SELECT *  FROM View_Delegates WHERE DelegateID = @LastId
END;
 

