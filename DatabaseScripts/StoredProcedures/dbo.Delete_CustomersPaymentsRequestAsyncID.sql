
CREATE proc [dbo].[Delete_CustomersPaymentsRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomersPaymentsRequest where AsyncID=@AsyncID 

