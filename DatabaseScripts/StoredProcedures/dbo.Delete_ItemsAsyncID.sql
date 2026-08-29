
CREATE proc [dbo].[Delete_ItemsAsyncID]  @AsyncID nvarchar(255) = null as delete from Items where AsyncID=@AsyncID 

