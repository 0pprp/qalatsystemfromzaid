
CREATE proc [dbo].[Delete_SelectItemCustomerAddRequestTempAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemCustomerAddRequestTemp where AsyncID=@AsyncID 

