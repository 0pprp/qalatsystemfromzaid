CREATE proc [dbo].[UpdatePasswordDelegate]
@UserID int = NULL,
@DelegateID int = NULL,
@AsyncID nvarchar(255) = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل كلمة سر المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateID)+N' من '+(select AsyncID from Delegates where DelegateID=@DelegateID)+N' الى '+@AsyncID+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
update Delegates set AsyncID=@AsyncID where DelegateID=@DelegateID

 

