
CREATE proc [dbo].[Delete_SelectDelegateAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectDelegate where AsyncID=@AsyncID 

