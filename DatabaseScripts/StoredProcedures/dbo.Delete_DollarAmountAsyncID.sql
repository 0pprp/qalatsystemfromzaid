
CREATE proc [dbo].[Delete_DollarAmountAsyncID]  @AsyncID nvarchar(255) = null as delete from DollarAmount where AsyncID=@AsyncID 

