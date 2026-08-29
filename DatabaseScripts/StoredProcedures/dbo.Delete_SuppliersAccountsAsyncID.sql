
CREATE proc [dbo].[Delete_SuppliersAccountsAsyncID]  @AsyncID nvarchar(255) = null as delete from SuppliersAccounts where AsyncID=@AsyncID 

