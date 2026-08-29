CREATE PROCEDURE [dbo].[WithdrawalFromBoxs_GetByDateByBoxID]
    @FromDate DATETIME,
    @ToDate DATETIME,
    @BoxID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM View_WithdrawalFromBox
    WHERE CONVERT(date, DateCreate)>=@FromDate and 
CONVERT(date, DateCreate)<=@ToDate
    AND (@BoxID IS NULL OR BoxID = @BoxID);
END;
  

