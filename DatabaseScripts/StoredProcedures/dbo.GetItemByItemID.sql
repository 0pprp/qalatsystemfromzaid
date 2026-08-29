CREATE proc [dbo].[GetItemByItemID]
@ItemID int = NULL
as
SELECT     * FROM      View_Items
where ItemID=@ItemID

