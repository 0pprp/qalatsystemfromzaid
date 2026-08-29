
CREATE PROCEDURE [dbo].[Delegates_Update]
    @DelegateID INT,
    @DelegateName NVARCHAR(100),
    @UserUpdateID INT,
    @Address NVARCHAR(100),
    @PhoneNumber NVARCHAR(100),
    @ReceiptName NVARCHAR(100),
    @AsyncID NVARCHAR(100) = NULL, 
    @Notes NVARCHAR(100)
AS
BEGIN
    IF @AsyncID IS NULL OR @AsyncID = ''
    BEGIN
        SELECT @AsyncID = AsyncID FROM Delegates WHERE DelegateID = @DelegateID;
    END
    UPDATE Delegates
    SET 
        DelegateName = @DelegateName,
        UserID = @UserUpdateID,
        Address = @Address,
        PhoneNumber = @PhoneNumber,
        AsyncID = @AsyncID,
        Notes = @Notes,
		ReceiptName = @ReceiptName
    WHERE DelegateID = @DelegateID;
			INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم تعديل المندوب  '+@DelegateName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
    SELECT *  
    FROM View_Delegates 
    WHERE DelegateID = @DelegateID;
END;


