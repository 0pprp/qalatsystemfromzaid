CREATE VIEW [dbo].[View_Box]
AS
WITH AddToBoxTotal AS (SELECT        BoxID, SUM(AmountDenar) AS TotalAmountDenar
                                                       FROM            dbo.View_AddToBox
                                                       GROUP BY BoxID), WithdrawalFromBoxTotal AS
    (SELECT        BoxID, SUM(AmountDenar) AS TotalAmountDenar
      FROM            dbo.View_WithdrawalFromBox
      GROUP BY BoxID)
    SELECT        b.BoxID, b.BoxName, b.AsyncState, b.AsyncID, b.BoxState, COALESCE (a.TotalAmountDenar, 0) - COALESCE (w.TotalAmountDenar, 0) AS AmountDenar, 0 AS SelectState
     FROM            dbo.Boxes AS b LEFT OUTER JOIN
                              AddToBoxTotal AS a ON b.BoxID = a.BoxID LEFT OUTER JOIN
                              WithdrawalFromBoxTotal AS w ON b.BoxID = w.BoxID

