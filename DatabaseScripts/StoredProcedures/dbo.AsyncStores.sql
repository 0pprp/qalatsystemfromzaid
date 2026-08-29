CREATE proc [dbo].[AsyncStores]
as
select * from Stores where AsyncState='false'

