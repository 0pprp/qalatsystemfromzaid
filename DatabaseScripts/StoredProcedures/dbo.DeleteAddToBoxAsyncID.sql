CREATE proc [dbo].[DeleteAddToBoxAsyncID]
@AddToBoxID int = NULL
as
Insert into DeleteData (AddToBoxAsyncID) values ((select AsyncID from AddToBox where AddToBoxID=@AddToBoxID))

