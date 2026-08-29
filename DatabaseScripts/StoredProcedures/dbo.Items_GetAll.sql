CREATE PROC [dbo].[Items_GetAll]  
    @StoreID INT = NULL,
    @ItemName NVARCHAR(100) = NULL,
    @ShowType NVARCHAR(100) = N'الجميع' 
AS
BEGIN
    SET NOCOUNT ON;

    IF @ShowType =  N'الجميع'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE 
            (@StoreID IS NULL OR StoreID = @StoreID)
            AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
    END

	IF @ShowType = N'المواد الحالية'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE 
            (@StoreID IS NULL OR StoreID = @StoreID)
            AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
            AND Quantity > 0;
    END

    IF @ShowType = N'المواد النافذة'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE 
            (@StoreID IS NULL OR StoreID = @StoreID)
            AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
            AND Quantity = 0;
    END

    IF @ShowType = N'الاكثر مبيعا'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE (@StoreID IS NULL OR StoreID = @StoreID)
        AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
        ORDER BY NumberOfItemsSales DESC; 
    END

    IF @ShowType = N'الاقل مبيعا'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE (@StoreID IS NULL OR StoreID = @StoreID)
        AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
        ORDER BY NumberOfItemsSales ASC; 
    END

    IF @ShowType = N'غير مباع'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE 
            (@StoreID IS NULL OR StoreID = @StoreID)
            AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
            AND NumberOfItemsSales = 0; 
    END

    IF @ShowType = N'على وشك النفاذ'
    BEGIN
        SELECT * 
        FROM View_Items 
        WHERE 
            (@StoreID IS NULL OR StoreID = @StoreID)
            AND (@ItemName IS NULL OR ItemName LIKE N'%' + @ItemName + N'%') and ItemState=1
            AND Quantity<=NotificationNumber;
    END
END


