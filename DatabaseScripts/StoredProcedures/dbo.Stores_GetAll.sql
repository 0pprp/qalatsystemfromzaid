CREATE proc [dbo].[Stores_GetAll]
@TextSearch nvarchar(100)
AS
BEGIN
    SELECT * FROM View_Stores where State='true'
	 AND (@TextSearch IS NULL OR 
             StoreName LIKE '%' + @TextSearch + '%' OR 
             StorePlace LIKE '%' + @TextSearch + '%' )
END


