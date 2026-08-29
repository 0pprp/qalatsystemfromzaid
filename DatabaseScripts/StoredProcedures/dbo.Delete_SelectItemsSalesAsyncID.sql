
CREATE proc [dbo].[Delete_SelectItemsSalesAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectItemsSales where AsyncID=@AsyncID 

