create   view [dbo].[View_TransferBoxs]
AS
SELECT        TB.TransferBoxID, TB.FromBoxID, TB.ToBoxID, TB.UserID, TB.Amount, TB.Notes, TB.DateModify, TB.DateCreate, TB.AsyncState, TB.AsyncID, FB.BoxName AS FromBoxName, TBX.BoxName AS ToBoxName, U.UserName, 
                         ISNULL(TB.Amount * 1448, 0) AS AmountDenar
FROM            dbo.TransferBoxs AS TB LEFT OUTER JOIN
                         dbo.Boxes AS FB ON TB.FromBoxID = FB.BoxID LEFT OUTER JOIN
                         dbo.Boxes AS TBX ON TB.ToBoxID = TBX.BoxID LEFT OUTER JOIN
                         dbo.Users AS U ON TB.UserID = U.UserID

