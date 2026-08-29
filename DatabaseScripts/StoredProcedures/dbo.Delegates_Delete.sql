
CREATE proc [dbo].[Delegates_Delete]
@DelegateID int,
@UserDeleteID int
as
update Delegates set DelegateState=0 where DelegateID=@DelegateID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

