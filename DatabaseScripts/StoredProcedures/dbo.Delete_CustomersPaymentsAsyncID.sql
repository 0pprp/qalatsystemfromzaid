
CREATE proc [dbo].[Delete_CustomersPaymentsAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomersPayments where AsyncID=@AsyncID 

