CREATE PROCEDURE [dbo].[TransferBoxs_GetByDate]
    @FromDate DATETIME,
    @ToDate DATETIME 
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM View_TransferBoxs
    WHERE CONVERT(date, DateCreate)>=@FromDate and 
    CONVERT(date, DateCreate)<=@ToDate
END;


