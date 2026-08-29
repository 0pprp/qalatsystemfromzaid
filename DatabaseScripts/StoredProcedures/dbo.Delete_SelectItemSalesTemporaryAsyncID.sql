
CREATE proc [dbo].[Delete_SelectItemSalesTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemSalesTemporary where AsyncID=@AsyncID 

