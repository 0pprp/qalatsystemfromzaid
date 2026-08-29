
CREATE proc [dbo].[Delete_GroupsAsyncID]  @AsyncID nvarchar(255) = null as delete from Groups where AsyncID=@AsyncID 

