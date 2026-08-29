CREATE proc [dbo].[InsertSelectDelegate]
@UserID int = NULL,
@DelegateFatherID int = NULL,
@DelegateChildID int = NULL
as
insert into SelectDelegate (DelegateFatherID,DelegateChildID,UserID,AsyncState,AsyncID) values (@DelegateFatherID,@DelegateChildID,@UserID,'false',NEWID())
 
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة صلاحية المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateChildID)+N' الى المندوب '+(select DelegateName from Delegates where DelegateID=@DelegateFatherID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

