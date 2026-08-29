
CREATE proc [dbo].[Delete_SelectItemDamageTemporaryAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemDamageTemporary where AsyncID=@AsyncID 

