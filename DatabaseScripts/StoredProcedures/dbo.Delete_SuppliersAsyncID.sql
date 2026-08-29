
CREATE proc [dbo].[Delete_SuppliersAsyncID]  @AsyncID nvarchar(255) = null as delete from Suppliers where AsyncID=@AsyncID 

