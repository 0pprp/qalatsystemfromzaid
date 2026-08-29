
CREATE proc [dbo].[Delete_SelectItemRestoreBuyTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemRestoreBuyTemporary where AsyncID=@AsyncID 

