CREATE proc [dbo].[DeleteDocumentAsyncID]
@DocumentID int = NULL
as
insert DeleteData (DocumentsAsyncID) values ((select AsyncID from Documents where DocumentID=@DocumentID))

