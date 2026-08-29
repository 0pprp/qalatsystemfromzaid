CREATE proc [dbo].[UpdateUpdateReceipt]
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
           ,N'تم تعديل صلاحية التعديل للمندوب '+(select DelegateName from Delegates where DelegateID=@DelegateID)+N' من '+(select convert(nvarchar(10),UpdateReceipt) from Delegates where DelegateID=@DelegateID)+N' الى '+(convert(nvarchar(10),@State))+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Delegates set UpdateReceipt=@State where DelegateID=@DelegateID

