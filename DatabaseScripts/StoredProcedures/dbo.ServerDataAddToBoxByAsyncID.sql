CREATE proc [dbo].[ServerDataAddToBoxByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from AddToBox where AsyncID=@AsyncID

