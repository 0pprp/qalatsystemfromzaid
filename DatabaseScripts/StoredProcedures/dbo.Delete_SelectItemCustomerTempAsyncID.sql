
CREATE proc [dbo].[Delete_SelectItemCustomerTempAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemCustomerTemp where AsyncID=@AsyncID 

