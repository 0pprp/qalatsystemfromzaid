CREATE proc [dbo].[ServerDataStoreByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Stores where AsyncID=@AsyncID

