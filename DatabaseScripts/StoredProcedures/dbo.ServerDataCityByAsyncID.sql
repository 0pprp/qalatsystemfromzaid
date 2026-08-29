CREATE proc [dbo].[ServerDataCityByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Cities where AsyncID=@AsyncID

