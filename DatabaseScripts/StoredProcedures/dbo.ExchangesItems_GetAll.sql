CREATE proc [dbo].[ExchangesItems_GetAll]
AS
BEGIN
    SELECT * FROM View_ExchangeItems where ExchangeItemsState='true';
END


