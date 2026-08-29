CREATE proc [dbo].[CheckQuantity]
@ItemID int = NULL
as
select ItemID,Quantity from Items
where ItemID=@ItemID

