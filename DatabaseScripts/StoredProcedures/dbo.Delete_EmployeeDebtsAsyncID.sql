
CREATE proc [dbo].[Delete_EmployeeDebtsAsyncID]  @AsyncID nvarchar(255) = null as delete from EmployeeDebts where AsyncID=@AsyncID 

