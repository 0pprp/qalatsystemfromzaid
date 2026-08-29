
CREATE proc [dbo].[Delete_DelegatesSalariesAsyncID]  @AsyncID nvarchar(255) = null as delete from DelegatesSalaries where AsyncID=@AsyncID 

