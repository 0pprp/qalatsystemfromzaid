CREATE proc [dbo].[AddToBoxs_GetByDateByBoxID]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @BoxID INT = NULL
AS
BEGIN
    SELECT * 
    FROM View_AddToBox 
    WHERE 
        (@FromDate IS NULL OR CONVERT(DATE, DateCreate) >= CONVERT(DATE, @FromDate))
        AND (@ToDate IS NULL OR CONVERT(DATE, DateCreate) <= CONVERT(DATE, @ToDate))
        AND (@BoxID IS NULL OR BoxID = @BoxID);
END


