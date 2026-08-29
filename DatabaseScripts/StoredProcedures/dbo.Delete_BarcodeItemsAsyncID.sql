
CREATE proc [dbo].[Delete_BarcodeItemsAsyncID]  @AsyncID nvarchar(255) = null as delete from BarcodeItems where AsyncID=@AsyncID 

