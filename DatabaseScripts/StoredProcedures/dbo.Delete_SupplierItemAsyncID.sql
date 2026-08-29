
CREATE proc [dbo].[Delete_SupplierItemAsyncID]  @AsyncID nvarchar(255) = null as delete from SupplierItem where AsyncID=@AsyncID 

