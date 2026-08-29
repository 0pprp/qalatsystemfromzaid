
CREATE proc [dbo].[Delete_SupplierItemRequestOldAsyncID]  @AsyncID nvarchar(255) = null as delete from SupplierItemRequestOld where AsyncID=@AsyncID 

