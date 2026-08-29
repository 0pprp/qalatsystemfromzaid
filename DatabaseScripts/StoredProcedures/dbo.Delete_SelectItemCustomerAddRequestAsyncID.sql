
CREATE proc [dbo].[Delete_SelectItemCustomerAddRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemCustomerAddRequest where AsyncID=@AsyncID 

