
CREATE proc [dbo].[Delete_DelegatesDebtsAsyncID]  @AsyncID nvarchar(255) = null as delete from DelegatesDebts where AsyncID=@AsyncID 

