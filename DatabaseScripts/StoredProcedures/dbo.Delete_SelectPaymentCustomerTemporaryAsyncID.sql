
CREATE proc [dbo].[Delete_SelectPaymentCustomerTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectPaymentCustomerTemporary where AsyncID=@AsyncID 

