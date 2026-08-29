CREATE proc [dbo].[DeleteDocument]
@DocumentID int = NULL,
@UserID int = NULL
as
exec DeleteDocumentAsyncID @DocumentID=@DocumentID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف السند '+(select DocumentType from Documents where DocumentID=@DocumentID)+N' من حساب '+(select FromAccount from Documents where DocumentID=@DocumentID)+N' الى حساب '+(select ToAccount from Documents where DocumentID=@DocumentID)+' المبلغ '+(select CONVERT(nvarchar(255),AmountDenar) from View_Documents where DocumentID=@DocumentID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from Documents where DocumentID=@DocumentID
 

