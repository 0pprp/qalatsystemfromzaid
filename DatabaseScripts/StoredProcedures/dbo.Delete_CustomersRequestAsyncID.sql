
CREATE proc [dbo].[Delete_CustomersRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomersRequest where AsyncID=@AsyncID 

