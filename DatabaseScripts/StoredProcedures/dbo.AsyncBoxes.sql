CREATE proc [dbo].[AsyncBoxes]
as
select * from Boxes where AsyncState='false'

