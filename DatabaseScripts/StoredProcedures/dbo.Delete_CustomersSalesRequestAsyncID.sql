
CREATE proc [dbo].[Delete_CustomersSalesRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomersSalesRequest where AsyncID=@AsyncID 

