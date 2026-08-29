CREATE proc [dbo].[CheckItemFind]
@ItemName nvarchar(255),
@StoreID int = NULL
as
select * from Items where ItemState='true' and ItemName=@ItemName and StoreID=@StoreID

