CREATE proc [dbo].[UpdateDelegate]
@DelegateID int = NULL,
@UserID int = NULL,
@CityID int = NULL,
@DelegateName nvarchar(255),
@Address nvarchar(255),
@PhoneNumber nvarchar(255),
@Notes nvarchar(max),
@ReceiptName nvarchar(255) ,
@AsyncID nvarchar(255)  
as
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل معلومات المندوب من '+(select DelegateName from Delegates where DelegateID=@DelegateID)+N' الى '+@DelegateName+N' وعنوانة من '+(select Address from Delegates where DelegateID=@DelegateID)+N' الى '+@Address+N' و رقم هاتفة من '+(select PhoneNumber from Delegates where DelegateID=@DelegateID)+N' الى '+@PhoneNumber+N' و اسم الجابي من '+(select ReceiptName from Delegates where DelegateID=@DelegateID)+N' الى '+@ReceiptName+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
UPDATE [dbo].[Delegates]
   SET [UserID] = @UserID 
      ,[CityID] = @CityID 
      ,[DelegateName] = @DelegateName 
      ,[Address] = @Address 
      ,[PhoneNumber] = @PhoneNumber 
      ,[Notes] = @Notes 
      ,[ReceiptName] = @ReceiptName 
      ,[AsyncID] = @AsyncID  
 WHERE  
 DelegateID=@DelegateID

