
CREATE proc [dbo].[Delete_SupplierRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SupplierRequest where AsyncID=@AsyncID 

