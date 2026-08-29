CREATE proc [dbo].[DeleteUser]
@UserIDDelete int = NULL,
@UserID int  = NULL
as
  
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المستخدم '+(select UserName from Users where UserID=@UserIDDelete)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Users set UserState='false' WHERE UserID=@UserIDDelete
 

