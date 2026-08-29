
CREATE proc [dbo].[Delete_HostHistoryAsyncID]  @AsyncID nvarchar(255) = null as delete from HostHistory where AsyncID=@AsyncID 

