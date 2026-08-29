
CREATE proc [dbo].[Delete_BuysItemsAsyncID]  @AsyncID nvarchar(255) = null as delete from BuysItems where AsyncID=@AsyncID 

