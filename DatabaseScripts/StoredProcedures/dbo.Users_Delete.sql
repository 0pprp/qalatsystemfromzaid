
CREATE proc [dbo].[Users_Delete]
@UserID int,
@UserDeleteID int
as
update Users set UserState=0 where UserID = @UserID
		INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف المستخدم '+(select UserName from Users where UserID=@UserID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() ) 

