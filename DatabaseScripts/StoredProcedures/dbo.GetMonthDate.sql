CREATE proc [dbo].[GetMonthDate]
@Number int
as
DECLARE @StartDate DATE = getutcdate()-30;
DECLARE @EndDate DATE = getutcdate()-1;
WITH DateCTE AS (
    SELECT @StartDate AS DateValue
    UNION ALL
    SELECT DATEADD(DAY, 1, DateValue)
    FROM DateCTE
    WHERE DateValue < @EndDate  
)
SELECT    DateValue
FROM DateCTE   order by DateValue desc  OFFSET @Number-1 ROWS  FETCH NEXT 1 ROW ONLY  ;

