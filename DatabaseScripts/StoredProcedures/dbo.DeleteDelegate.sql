CREATE proc [dbo].[DeleteDelegate]
@DelegateID int = NULL,
@UserID int = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Delegates set DelegateState='false' where DelegateID=@DelegateID

