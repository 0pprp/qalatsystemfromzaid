
CREATE proc [dbo].[Delete_SelectItemSalesTemporaryRequestAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemSalesTemporaryRequest where AsyncID=@AsyncID 

