
CREATE proc [dbo].[Delete_SelectItemBuyTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemBuyTemporary where AsyncID=@AsyncID 

