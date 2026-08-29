CREATE proc [dbo].[AsyncItems]
as
select * from Items where AsyncState='false'

