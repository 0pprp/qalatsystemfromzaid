
CREATE proc [dbo].[Delete_DamagedItemsAsyncID]  @AsyncID nvarchar(255) = null as delete from DamagedItems where AsyncID=@AsyncID 

