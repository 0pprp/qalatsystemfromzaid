create   view [dbo].[WeekDateView]
AS
WITH DateCTE AS (SELECT        getutcdate() - 9 AS DateValue
                                         UNION ALL
                                         SELECT        DATEADD(DAY, 1, DateValue) AS Expr1
                                         FROM            DateCTE AS DateCTE_2
                                         WHERE        (DateValue <= getutcdate() - 1))
    SELECT        DateValue
     FROM            DateCTE AS DateCTE_1

