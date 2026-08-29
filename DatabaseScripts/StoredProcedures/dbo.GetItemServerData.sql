CREATE proc [dbo].[GetItemServerData]
as
select ItemID,Quantity,AsyncID,ItemState from Items

