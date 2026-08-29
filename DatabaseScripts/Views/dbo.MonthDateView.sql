create   view [dbo].[MonthDateView] AS
WITH DateCTE AS (
    SELECT getutcdate()-30 AS DateValue
    UNION ALL
    SELECT DATEADD(DAY, 1, DateValue)
    FROM DateCTE
    WHERE DateValue <= getutcdate()-1
)
SELECT DateValue
FROM DateCTE     ;

