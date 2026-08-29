
CREATE proc [dbo].[Delete_CustomerAddRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomerAddRequest where AsyncID=@AsyncID 

