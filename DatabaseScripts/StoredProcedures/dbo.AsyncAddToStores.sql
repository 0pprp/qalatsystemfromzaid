CREATE proc [dbo].[AsyncAddToStores]
as
select * from AddToStores where AsyncState='false'

