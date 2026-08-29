
CREATE proc [dbo].[Delete_BuysAsyncID]  @AsyncID nvarchar(255) = null as delete from Buys where AsyncID=@AsyncID 

