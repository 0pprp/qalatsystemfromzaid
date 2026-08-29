
CREATE proc [dbo].[Delete_SelectedBoxesAsyncID]  @AsyncID nvarchar(255) = null as delete from SelectedBoxes where AsyncID=@AsyncID 

