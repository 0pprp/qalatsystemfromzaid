
CREATE proc [dbo].[Delete_EmployeesSalariesAsyncID]  @AsyncID nvarchar(255) = null as delete from EmployeesSalaries where AsyncID=@AsyncID 

