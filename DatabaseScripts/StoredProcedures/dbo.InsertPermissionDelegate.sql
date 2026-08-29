CREATE proc [dbo].[InsertPermissionDelegate]
@DelegateFatherID int = NULL,
@DelegateChildID int = NULL,
@UserID int = NULL
as
 
INSERT INTO [dbo].[SelectDelegate]
           ([DelegateFatherID]
           ,[DelegateChildID]
           ,[UserID]
           ,[AsyncState] 
		   ,[AsyncID])
     VALUES
           (@DelegateFatherID 
           ,@DelegateChildID 
           ,@UserID 
           ,'false'
		   ,NEWID())
 
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة صلاحية المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateFatherID)+N' الى التحكم بكافة صلاحيات المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateChildID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

