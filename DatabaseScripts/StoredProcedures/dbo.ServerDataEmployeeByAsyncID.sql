CREATE proc [dbo].[ServerDataEmployeeByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Employees where AsyncID=@AsyncID

