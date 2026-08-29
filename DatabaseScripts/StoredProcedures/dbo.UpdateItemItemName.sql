CREATE proc [dbo].[UpdateItemItemName]
@ItemID int = NULL,
@ItemName nvarchar(255)
as
update Items set ItemName=@ItemName where ItemID=@ItemID

