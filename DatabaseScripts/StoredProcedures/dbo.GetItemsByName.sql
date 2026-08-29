 
CREATE proc [dbo].[GetItemsByName]
@ItemName nvarchar(255)
as
SELECT     * FROM      View_Items where ItemState='true' and ItemName like  N'%'+@ItemName+N'%'
 

