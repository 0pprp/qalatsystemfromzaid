create   view [dbo].[View_Category]
AS
SELECT        dbo.Category.CategoryID, dbo.Category.UserID, dbo.Category.CategoryName, dbo.Category.CategoryState, dbo.Category.AsyncID, dbo.Category.AsyncState, dbo.Users.UserName AS UserName, 
                         ISNULL(COUNT(dbo.ItemMerchant.CategoryID), 0) AS NumberOfItem
FROM            dbo.Category LEFT OUTER JOIN
                         dbo.Users ON dbo.Category.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.ItemMerchant ON dbo.Category.CategoryID = dbo.ItemMerchant.CategoryID
WHERE        (dbo.Category.CategoryState = 'true')
GROUP BY dbo.Category.CategoryID, dbo.Category.UserID, dbo.Category.CategoryName, dbo.Category.CategoryState, dbo.Category.AsyncID, dbo.Category.AsyncState, dbo.Users.UserName

