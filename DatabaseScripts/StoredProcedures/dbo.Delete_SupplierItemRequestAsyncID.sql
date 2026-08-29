
CREATE proc [dbo].[Delete_SupplierItemRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SupplierItemRequest where AsyncID=@AsyncID 

