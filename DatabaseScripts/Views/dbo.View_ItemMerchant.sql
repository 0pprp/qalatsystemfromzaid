create   view [dbo].[View_ItemMerchant]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users
                                           WHERE        (UserState = 'true')), MerchantData AS
    (SELECT        MerchantID, MerchantName
      FROM            dbo.Merchant
      WHERE        (MerchantState = 'true')), CategoryData AS
    (SELECT        CategoryID, CategoryName
      FROM            dbo.Category
      WHERE        (CategoryState = 'true')), ImageData AS
    (SELECT        ItemMerchantID, ISNULL(Image, '') AS Image
      FROM            dbo.ItemMerchantImage)
    SELECT        dbo.ItemMerchant.ItemMerchantID, dbo.ItemMerchant.MerchantID, dbo.ItemMerchant.UserID, dbo.ItemMerchant.ItemMerchantName, dbo.ItemMerchant.Specifications, dbo.ItemMerchant.Notes, 
                              dbo.ItemMerchant.ItemMerchantState, dbo.ItemMerchant.CategoryID, dbo.ItemMerchant.Price, dbo.ItemMerchant.AsyncID, dbo.ItemMerchant.AsyncState, UserData_1.UserName, MerchantData_1.MerchantName, 
                              CategoryData_1.CategoryName,
                                  (SELECT        TOP (1) Image
                                    FROM            ImageData AS ImageData_1
                                    WHERE        (ItemMerchantID = dbo.ItemMerchant.ItemMerchantID)) AS Image
     FROM            dbo.ItemMerchant LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.ItemMerchant.UserID = UserData_1.UserID LEFT OUTER JOIN
                              MerchantData AS MerchantData_1 ON dbo.ItemMerchant.MerchantID = MerchantData_1.MerchantID LEFT OUTER JOIN
                              CategoryData AS CategoryData_1 ON dbo.ItemMerchant.CategoryID = CategoryData_1.CategoryID
     WHERE        (dbo.ItemMerchant.ItemMerchantState = 'true')

