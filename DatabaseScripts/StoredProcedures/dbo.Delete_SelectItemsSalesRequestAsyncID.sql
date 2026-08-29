
CREATE proc [dbo].[Delete_SelectItemsSalesRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsSalesRequest where AsyncID=@AsyncID 

