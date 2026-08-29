
CREATE proc [dbo].[Delete_SelectItemCustomerAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemCustomer where AsyncID=@AsyncID 

