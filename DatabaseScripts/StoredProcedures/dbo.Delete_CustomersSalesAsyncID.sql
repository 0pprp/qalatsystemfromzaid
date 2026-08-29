
CREATE proc [dbo].[Delete_CustomersSalesAsyncID]  @AsyncID nvarchar(255) = null as delete from CustomersSales where AsyncID=@AsyncID 

