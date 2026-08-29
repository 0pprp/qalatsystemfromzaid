CREATE proc [dbo].[ServerDataBoxByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Boxes where AsyncID=@AsyncID

