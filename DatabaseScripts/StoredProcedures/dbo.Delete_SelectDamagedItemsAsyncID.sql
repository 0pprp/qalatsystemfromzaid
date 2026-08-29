
CREATE proc [dbo].[Delete_SelectDamagedItemsAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectDamagedItems where AsyncID=@AsyncID 

