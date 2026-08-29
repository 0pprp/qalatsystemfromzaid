
CREATE proc [dbo].[Delete_EmployeesAsyncID]  @AsyncID nvarchar(255) = null as delete from Employees where AsyncID=@AsyncID 

