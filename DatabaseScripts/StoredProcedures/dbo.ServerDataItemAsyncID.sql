CREATE proc [dbo].[ServerDataItemAsyncID]
@AsyncID nvarchar(255) = null
as
select top 1 * from Items where AsyncID=@AsyncID

