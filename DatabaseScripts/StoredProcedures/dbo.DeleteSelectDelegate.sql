CREATE proc [dbo].[DeleteSelectDelegate]
@UserID int = NULL,
@DelegateFatherID int = NULL,
@DelegateChildID int = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف صلاحية المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateChildID)+N' على المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateFatherID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
delete from SelectDelegate where DelegateFatherID=@DelegateFatherID and DelegateChildID=@DelegateChildID
 

