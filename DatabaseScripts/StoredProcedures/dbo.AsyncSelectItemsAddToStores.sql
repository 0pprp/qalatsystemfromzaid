CREATE proc [dbo].[AsyncSelectItemsAddToStores]
as
select * from SelectItemsAddToStores where AsyncState='false'

