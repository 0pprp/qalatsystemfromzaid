
CREATE proc [dbo].[Delete_CustomersAsyncID]  @AsyncID nvarchar(255) = null as delete from Customers where AsyncID=@AsyncID 

