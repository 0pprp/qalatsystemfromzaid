CREATE proc [dbo].[UpdateAmountDayItem]
@ItemID int = NULL,
@AmountDay float
as
update Items set AmountDay=@AmountDay where ItemID=@ItemID

