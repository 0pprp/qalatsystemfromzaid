CREATE proc [dbo].[UpdateDeleteReceipt]
@DelegateID int = NULL,
@State bit,
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
           ,N'تم تغيير صلاحية حذف التسديد للمندوب '+(select DelegateName from Delegates where DelegateID=@DelegateID)+N' من '+(select  CONVERT(nvarchar(10), DeleteReceipt)   from Delegates where DelegateID=@DelegateID)+N'  الى '+(CONVERT(nvarchar(10), @State))+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Delegates set DeleteReceipt=@State where DelegateID=@DelegateID

