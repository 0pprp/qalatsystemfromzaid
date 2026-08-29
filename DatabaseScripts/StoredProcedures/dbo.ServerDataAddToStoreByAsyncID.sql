CREATE proc [dbo].[ServerDataAddToStoreByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from AddToStores where AsyncID=@AsyncID

