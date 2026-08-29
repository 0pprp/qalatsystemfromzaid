
CREATE proc [dbo].[Delete_UsersAsyncID]  @AsyncID nvarchar(255) = null as delete from Users where AsyncID=@AsyncID 

