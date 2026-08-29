
CREATE proc [dbo].[Delete_TransferBoxsAsyncID]  @AsyncID nvarchar(255) = null as delete from TransferBoxs where AsyncID=@AsyncID 

