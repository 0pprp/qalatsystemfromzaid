
CREATE proc [dbo].[Delete_CustomerOldRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerOldRequest where AsyncID=@AsyncID 

