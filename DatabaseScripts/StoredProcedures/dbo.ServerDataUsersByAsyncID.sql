CREATE proc [dbo].[ServerDataUsersByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Users where AsyncID=@AsyncID

